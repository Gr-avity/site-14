using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.GameTicking.Events;
using Content.Server.Station.Events;
using Content.Shared._SCP.Tickets;
using Content.Shared.CCVar;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Threading.Tasks;

namespace Content.Server._SCP.Tickets;

public sealed partial class TicketSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private ISawmill _sawmill = default!;

    // In-memory cache of paid hours per player (UserId) to track new hours that need tickets
    private readonly Dictionary<NetUserId, int> _paidHoursCache = new();
    // In-memory cache of ticket modifiers per player
    private readonly Dictionary<NetUserId, float> _modifierCache = new();
    // In-memory cache of ticket balances per player
    private readonly Dictionary<NetUserId, int> _balanceCache = new();

    // Flags for controlling processing
    private TimeSpan _nextTicketTick;
    private bool _needsProcessing;
    private bool _processing;

    public override void Initialize()
    {
        _sawmill = LogManager.GetSawmill("scp_tickets");

        _net.RegisterNetMessage<MsgTicketInfo>();

        _playTimeTracking.SessionPlayTimeUpdated += OnPlayTimeUpdated;
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

        SubscribeLocalEvent<StationJobsGetCandidatesEvent>(OnStationJobsGetCandidates);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
        SubscribeLocalEvent<IsRoleAllowedEvent>(OnIsRoleAllowed);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    public override void Shutdown()
    {
        _playTimeTracking.SessionPlayTimeUpdated -= OnPlayTimeUpdated;
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayTimeUpdated(ICommonSession session)
    {
        _needsProcessing = true;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
        {
            InvalidateCache(e.Session.UserId);
        }
        else if (e.NewStatus == SessionStatus.InGame)
        {
            SendTicketInfo(e.Session);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_processing)
            return;

        var realTime = _gameTiming.RealTime;
        if (!_needsProcessing && realTime < _nextTicketTick)
            return;

        _processing = true;
        _needsProcessing = false;
        _nextTicketTick = realTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.TicketTickIntervalSec));

        ProcessTicketCycleAsync();
    }

    private async void ProcessTicketCycleAsync()
    {
        try
        {
            foreach (var session in _playerManager.Sessions)
            {
                await ProcessPlayerTicketsAsync(session);
            }
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Error in ticket processing cycle: {ex}");
        }
        finally
        {
            _processing = false;
        }
    }

    private async Task ProcessPlayerTicketsAsync(ICommonSession session)
    {
        try
        {
            var userId = session.UserId;
            var totalHours = (int) _playTimeTracking
                .GetPlayTimeForTracker(session, PlayTimeTrackingShared.TrackerOverall)
                .TotalHours;

            if (!_paidHoursCache.TryGetValue(userId, out var paidHours))
            {
                // First time we see this player this session — don't pay for old hours,
                // otherwise reconnect/restart would grant tickets for already-played time.
                _paidHoursCache[userId] = totalHours;
                paidHours = totalHours;
            }

            var newHours = totalHours - paidHours;
            if (newHours <= 0)
                return;

            await EnsureLoadedAsync(userId);

            var modifier = _modifierCache.GetValueOrDefault(userId, 1.0f);
            var rate = _cfg.GetCVar(CCVars.TicketBaseRatePerHour);
            var modifierRounded = Math.Round((double) modifier, 2);
            var tickets = (int) Math.Floor(newHours * modifierRounded * rate);
            if (tickets > 0)
            {
                await _db.AddTicketsAsync(userId, tickets);
                _balanceCache[userId] = _balanceCache.GetValueOrDefault(userId) + tickets;
            }

            _paidHoursCache[userId] = totalHours;

            // Automatic modifier increase: +step per full ModifierHoursStep hours.
            var hoursStep = _cfg.GetCVar(CCVars.TicketModifierHoursStep);
            if (hoursStep > 0)
            {
                var step = _cfg.GetCVar(CCVars.TicketModifierStep);
                var max = _cfg.GetCVar(CCVars.TicketModifierMax);
                // Formula from total hours — no accumulated float error; round to 2 decimals for clean values.
                var target = MathF.Min(MathF.Round(1.0f + (totalHours / hoursStep) * step, 2), max);
                if (target > modifier)
                {
                    await _db.SetTicketModifierAsync(userId, target);
                    _modifierCache[userId] = target;
                }
            }

            TryPushTicketInfo(userId);
        }
        catch (InvalidOperationException)
        {
            // Play time info is not yet loaded for this player — skip this tick.
            return;
        }
    }

    /// <summary>
    /// Preloads ticket caches for all given sessions. Called from GameTicker.SpawnPlayers
    /// before AssignJobs so that candidate filtering sees up-to-date balances.
    /// </summary>
    public async Task PreloadTicketsAsync(IEnumerable<ICommonSession> sessions)
    {
        foreach (var session in sessions)
        {
            try
            {
                await EnsureLoadedAsync(session.UserId);
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Failed to preload tickets for {session}: {ex}");
            }
        }
    }

    #region Ticket queue — roundstart candidate filtering

    private void OnStationJobsGetCandidates(ref StationJobsGetCandidatesEvent ev)
    {
        if (!_balanceCache.TryGetValue(ev.Player, out var balance))
            return; // Not preloaded — leave candidates untouched.

        ev.Jobs.RemoveAll(job =>
        {
            if (!_prototypeManager.TryIndex<JobPrototype>(job, out var proto))
                return false;

            return proto.TicketOnly && proto.TicketCost > balance;
        });
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        if (!_balanceCache.TryGetValue(ev.Player.UserId, out var balance))
            return;

        foreach (var proto in _prototypeManager.EnumeratePrototypes<JobPrototype>())
        {
            if (proto.TicketOnly && proto.TicketCost > balance)
                ev.Jobs.Add(proto.ID);
        }
    }

    #endregion

    #region Ticket queue — latejoin gate

    private void OnIsRoleAllowed(ref IsRoleAllowedEvent ev)
    {
        if (ev.Jobs == null)
            return;

        if (!_balanceCache.TryGetValue(ev.Player.UserId, out var balance))
            return;

        foreach (var jobId in ev.Jobs)
        {
            if (!_prototypeManager.TryIndex<JobPrototype>(jobId, out var proto))
                continue;

            if (proto.TicketOnly && proto.TicketCost > balance)
            {
                ev.Cancelled = true;
                _chatManager.DispatchServerMessage(ev.Player,
                    Loc.GetString("scp-tickets-insufficient",
                        ("role", proto.LocalizedName),
                        ("cost", proto.TicketCost),
                        ("balance", balance)));
                return;
            }
        }
    }

    #endregion

    #region Ticket spending on spawn

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null)
            return;

        if (!_prototypeManager.TryIndex<JobPrototype>(ev.JobId, out var proto))
            return;

        if (!proto.TicketOnly || proto.TicketCost <= 0)
            return;

        if (!_balanceCache.TryGetValue(ev.Player.UserId, out var balance) || balance < proto.TicketCost)
            return;

        SpendTickets(ev.Player, proto);
    }

    private async void SpendTickets(ICommonSession player, JobPrototype job)
    {
        try
        {
            await AddTicketsAsync(player.UserId, -job.TicketCost);
            var newBalance = _balanceCache.GetValueOrDefault(player.UserId);

            _chatManager.DispatchServerMessage(player,
                Loc.GetString("scp-tickets-spent",
                    ("cost", job.TicketCost),
                    ("role", job.LocalizedName),
                    ("balance", newBalance)));
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to spend tickets for {player}: {ex}");
        }
    }

    #endregion

    #region Client info push

    private async void SendTicketInfo(ICommonSession player)
    {
        try
        {
            await EnsureLoadedAsync(player.UserId);

            var msg = new MsgTicketInfo
            {
                Balance = _balanceCache.GetValueOrDefault(player.UserId),
                Modifier = _modifierCache.GetValueOrDefault(player.UserId, 1.0f),
            };

            _net.ServerSendMessage(msg, player.Channel);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to send ticket info to {player}: {ex}");
        }
    }

    private void TryPushTicketInfo(NetUserId userId)
    {
        if (_playerManager.TryGetSessionById(userId, out var session))
            SendTicketInfo(session);
    }

    #endregion

    #region Cache / public API

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

        TryPushTicketInfo(userId);
    }

    public async Task SetTicketModifierAsync(NetUserId userId, float modifier)
    {
        await EnsureLoadedAsync(userId);

        // Clamp the modifier to the allowed range.
        modifier = Math.Clamp(modifier, 1.0f, _cfg.GetCVar(CCVars.TicketModifierMax));
        modifier = (float) Math.Round((double) modifier, 2);
        await _db.SetTicketModifierAsync(userId, modifier);
        _modifierCache[userId] = modifier;

        TryPushTicketInfo(userId);
    }

    public async Task<int> GetBalanceAsync(NetUserId userId)
    {
        await EnsureLoadedAsync(userId);

        return _balanceCache.GetValueOrDefault(userId);
    }

    public async Task SetTicketBalanceAsync(NetUserId userId, int balance)
    {
        await EnsureLoadedAsync(userId);

        // Clamp the balance to be non-negative
        balance = Math.Max(0, balance);
        await _db.SetTicketBalanceAsync(userId, balance);
        _balanceCache[userId] = balance;

        TryPushTicketInfo(userId);
    }

    public void InvalidateCache(NetUserId userId)
    {
        _paidHoursCache.Remove(userId);
        _modifierCache.Remove(userId);
        _balanceCache.Remove(userId);
    }

    #endregion
}
