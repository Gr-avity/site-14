using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._SCP.Tickets.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed partial class MassModifyTicketsCommand : LocalizedEntityCommands
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "scp_massmodifytickets";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Loc.GetString("cmd-scp-massmodifytickets-error-usage"));
            return;
        }

        if (!int.TryParse(args[0], out var amount) || amount <= 0)
        {
            shell.WriteError(Loc.GetString("cmd-scp-massmodifytickets-error-positive"));
            return;
        }

        var system = EntityManager.System<TicketSystem>();

        // Apply to all sessions
        var playerCount = 0;
        foreach (var session in _playerManager.Sessions)
        {
            try
            {
                await system.AddTicketsAsync(session.UserId, amount);
                playerCount++;
            }
            catch
            {
                // Skip sessions where tickets can't be added
            }
        }

        _adminLogger.Add(
            LogType.AdminCommands,
            LogImpact.Medium,
            $"{shell.Player} mass modified tickets for {playerCount} players: +{amount} each"
        );

        var massGiveAnnounce = _cfg.GetCVar(CCVars.TicketMassGiveAnnounce);
        if (massGiveAnnounce)
        {
            var announcement = Loc.GetString(
                "cmd-scp-massmodifytickets-announcement",
                ("count", playerCount.ToString()),
                ("amount", amount.ToString())
            );
            _chatManager.DispatchServerAnnouncement(announcement);
        }

        var result = Loc.GetString(
            "cmd-scp-massmodifytickets-result",
            ("count", playerCount.ToString()),
            ("amount", amount.ToString())
        );
        shell.WriteLine(result);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }
}
