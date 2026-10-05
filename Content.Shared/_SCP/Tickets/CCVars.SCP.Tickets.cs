using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Base rate of tickets earned per full hour of playtime.
    /// </summary>
    public static readonly CVarDef<float> TicketBaseRatePerHour =
        CVarDef.Create("scp_tickets.base_rate_per_hour", 1.0f, CVar.SERVER);

    /// <summary>
    /// Interval in seconds between ticket system ticks.
    /// </summary>
    public static readonly CVarDef<int> TicketTickIntervalSec =
        CVarDef.Create("scp_tickets.tick_interval_sec", 30, CVar.SERVER);

    /// <summary>
    /// Step increase for ticket modifier per hours played.
    /// </summary>
    public static readonly CVarDef<float> TicketModifierStep =
        CVarDef.Create("scp_tickets.modifier_step", 0.15f, CVar.SERVER);

    /// <summary>
    /// Number of hours played required to increase ticket modifier by a step.
    /// </summary>
    public static readonly CVarDef<int> TicketModifierHoursStep =
        CVarDef.Create("scp_tickets.modifier_hours_step", 100, CVar.SERVER);

    /// <summary>
    /// Maximum allowed ticket modifier value.
    /// </summary>
    public static readonly CVarDef<float> TicketModifierMax =
        CVarDef.Create("scp_tickets.modifier_max", 10.0f, CVar.SERVER);

    /// <summary>
    /// Whether to announce mass ticket give to all players.
    /// </summary>
    public static readonly CVarDef<bool> TicketMassGiveAnnounce =
        CVarDef.Create("scp_tickets.mass_give_announce", true, CVar.SERVER);
}