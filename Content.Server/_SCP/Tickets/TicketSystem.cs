using Content.Server.Database;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Player;
using System.Threading.Tasks;

namespace Content.Server._SCP.Tickets;

public sealed partial class TicketSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    private ISawmill _sawmill = default!;

    // In-memory cache of paid hours per player (UserId) to track new hours that need tickets
    private readonly Dictionary<NetUserId, int> _paidHoursCache = new();
    // In-memory cache of ticket modifiers per player
    private readonly Dictionary<NetUserId, float> _modifierCache = new();
    // In-memory cache of ticket balances per player
    private readonly Dictionary<NetUserId, int> _balanceCache = new();

    // Flags for controlling processing
    private TimeSpan _nextTicketTick = TimeSpan.Zero;
    private bool _needsProcessing;
    private bool _processing;

    public override void Initialize()
    {
        _sawmill = LogManager.GetSawmill("scp.tickets");
        _playTimeTracking.SessionPlayTimeUpdated += OnPlayTimeUpdated;
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        _playTimeTracking.SessionPlayTimeUpdated -= OnPlayTimeUpdated;
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public override void Update(float frameTime)
    {
        // Run processing cycle if either:
        // - we have pending playtime updates (from SessionPlayTimeUpdated), OR
        // - it's time for the periodic tick
        if (!_processing && (_needsProcessing || _gameTiming.RealTime >= _nextTicketTick))
        {
            _processing = true;
            // Fire-and-forget the processing cycle
            ProcessTicketCycleAsync();
        }
    }

    private void OnPlayTimeUpdated(ICommonSession session)
    {
        // Signal that we have pending playtime updates to process
        _needsProcessing = true;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
        {
            InvalidateCache(args.Session.UserId);
        }
    }

    private async void ProcessTicketCycleAsync()
    {
        try
        {
            var baseRate = _cfg.GetCVar(CCVars.TicketBaseRatePerHour);
            var modifierStep = _cfg.GetCVar(CCVars.TicketModifierStep);
            var modifierHoursStep = _cfg.GetCVar(CCVars.TicketModifierHoursStep);
            var modifierMax = _cfg.GetCVar(CCVars.TicketModifierMax);
            var updateTasks = new List<Task>();

            // Process all connected sessions
            foreach (var session in _playerManager.Sessions)
            {
                try
                {
                    var task = ProcessPlayerTicketsAsync(session, baseRate, modifierStep, modifierHoursStep, modifierMax);
                    updateTasks.Add(task);
                }
                catch (Exception ex)
                {
                    _sawmill.Error($"Error processing tickets for session {session.Name}: {ex}");
                }
            }

            // Wait for all database operations to complete
            if (updateTasks.Count > 0)
            {
                await Task.WhenAll(updateTasks);
            }

            // Reset the needsProcessing flag (it might have been set again during processing by another event)
            _needsProcessing = false;

            // Set the next ticket tick time to be tickInterval seconds from now
            var tickInterval = _cfg.GetCVar(CCVars.TicketTickIntervalSec);
            _nextTicketTick = _gameTiming.RealTime + TimeSpan.FromSeconds(tickInterval);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Error in ProcessTicketCycleAsync: {ex}");
        }
        finally
        {
            _processing = false;
        }
    }

    private async Task ProcessPlayerTicketsAsync(
        ICommonSession session,
        float baseRate,
        float modifierStep,
        int modifierHoursStep,
        float modifierMax)
    {
        try
        {
            // Get overall play time - may throw if data not loaded (e.g., player just connected)
            var totalPlayTime = _playTimeTracking.GetPlayTimeForTracker(session, PlayTimeTrackingShared.TrackerOverall);
            var totalHours = (int)totalPlayTime.TotalHours;
            var userId = session.UserId;

            // Get cached values or load from DB
            if (!_paidHoursCache.TryGetValue(userId, out var paidHours))
            {
                // First time seeing this player: consider all hours up to now as already paid
                // This prevents awarding for hours accumulated before this connection
                paidHours = totalHours;
                _paidHoursCache[userId] = paidHours;
            }

            if (!_modifierCache.TryGetValue(userId, out var currentModifier))
            {
                // Load from DB if not in cache
                var (balance, modifier) = await _db.GetTicketsAsync(userId);
                _balanceCache[userId] = balance;
                _modifierCache[userId] = modifier;
                currentModifier = modifier;
            }

            // Calculate new hours to award tickets for
            var newHours = Math.Max(0, totalHours - paidHours);

            if (newHours == 0)
                return;

            var currentBalance = _balanceCache[userId];

            // Calculate auto-computed modifier based on total hours played
            var autoComputedModifier = Math.Clamp(
                1.0f + modifierStep * MathF.Floor(totalHours / (float)modifierHoursStep),
                1.0f,
                modifierMax
            );

            // Use the maximum of stored modifier and auto-computed modifier
            var finalModifier = Math.Max(currentModifier, autoComputedModifier);

            // Calculate tickets to award
            var ticketsToAdd = (int)Math.Floor(newHours * baseRate * finalModifier);

            if (ticketsToAdd > 0)
            {
                // Add tickets to DB
                await _db.AddTicketsAsync(userId, ticketsToAdd);

                // Update cache
                _balanceCache[userId] = currentBalance + ticketsToAdd;
                _paidHoursCache[userId] = totalHours;
            }

            // Update modifier in DB if it changed
            if (currentModifier != finalModifier)
            {
                await _db.SetTicketModifierAsync(userId, finalModifier);
                _modifierCache[userId] = finalModifier;
            }
        }
        catch (InvalidOperationException)
        {
            // Session data not loaded yet (e.g., player just connected) - skip
            // The SessionPlayTimeUpdated event will trigger processing next tick
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Unexpected error processing tickets for session {session.Name}: {ex}");
        }
    }

    private async Task EnsureLoadedAsync(NetUserId userId)
    {
        if (_modifierCache.ContainsKey(userId) && _balanceCache.ContainsKey(userId))
            return;

        var (balance, modifier) = await _db.GetTicketsAsync(userId);
        _balanceCache[userId] = balance;
        _modifierCache[userId] = modifier;
    }

    // Public API for external systems to use
    public async Task<(int balance, float modifier)> GetTicketsAsync(NetUserId userId)
    {
        await EnsureLoadedAsync(userId);

        return (_balanceCache.GetValueOrDefault(userId), _modifierCache.GetValueOrDefault(userId, 1.0f));
    }

    public async Task AddTicketsAsync(NetUserId userId, int amount)
    {
        await EnsureLoadedAsync(userId);

        await _db.AddTicketsAsync(userId, amount);
        _balanceCache[userId] = _balanceCache.GetValueOrDefault(userId) + amount;
    }

    public async Task SetTicketModifierAsync(NetUserId userId, float modifier)
    {
        await EnsureLoadedAsync(userId);

        // Clamp the modifier to the allowed range.
        modifier = Math.Clamp(modifier, 1.0f, _cfg.GetCVar(CCVars.TicketModifierMax));
        await _db.SetTicketModifierAsync(userId, modifier);
        _modifierCache[userId] = modifier;
    }

    public async Task<int> GetBalanceAsync(NetUserId userId)
    {
        await EnsureLoadedAsync(userId);

        return _balanceCache.GetValueOrDefault(userId);
    }

    // _SCP: Added for ticket balance manipulation
    public async Task SetTicketBalanceAsync(NetUserId userId, int balance)
    {
        await EnsureLoadedAsync(userId);

        // Clamp the balance to be non-negative
        balance = Math.Max(0, balance);
        await _db.SetTicketBalanceAsync(userId, balance);
        _balanceCache[userId] = balance;
    }

    public void InvalidateCache(NetUserId userId)
    {
        _paidHoursCache.Remove(userId);
        _modifierCache.Remove(userId);
        _balanceCache.Remove(userId);
    }
}
