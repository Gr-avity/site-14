using System.Globalization;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Server._SCP.Tickets.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed partial class ModifyTicketsCommand : LocalizedEntityCommands
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IPlayerLocator _locator = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "scp_modifytickets";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        try
        {
            if (args.Length < 3)
            {
                shell.WriteError(Loc.GetString("cmd-scp-modifytickets-error-usage"));
                return;
            }

            var targetArg = args[0];
            var operation = args[1].ToLowerInvariant();
            var amountStr = args[2];

            if (operation != "add" && operation != "subtract")
            {
                shell.WriteError(Loc.GetString("cmd-scp-modifytickets-error-invalid-operation"));
                return;
            }

            if (!float.TryParse(amountStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)
                || !float.IsFinite(amount) || amount <= 0.0f)
            {
                shell.WriteError(Loc.GetString("cmd-scp-modifytickets-error-invalid-amount"));
                return;
            }

            var data = await _locator.LookupIdByNameOrIdAsync(targetArg);
            if (data == null)
            {
                shell.WriteError(Loc.GetString("cmd-scp-modifytickets-error-player-not-found", ("player", targetArg)));
                return;
            }

            var userId = new NetUserId(data.UserId);

            var system = EntityManager.System<TicketSystem>();
            var (currentBalance, currentModifier) = await system.GetTicketsAsync(userId);

            float newModifier;
            if (operation == "add")
            {
                newModifier = MathF.Min(currentModifier + amount, _cfg.GetCVar(CCVars.TicketModifierMax));
            }
            else
            {
                newModifier = MathF.Max(currentModifier - amount, 1.0f);
            }

            await system.SetTicketModifierAsync(userId, newModifier);

            _adminLogger.Add(
                LogType.AdminCommands,
                LogImpact.Medium,
                $"{shell.Player} modified ticket modifier for {data.Username}: {currentModifier} -> {newModifier}"
            );

            var result = Loc.GetString(
                "cmd-scp-modifytickets-result",
                ("player", data.Username),
                ("old", currentModifier.ToString("F2")),
                ("new", newModifier.ToString("F2"))
            );
            shell.WriteLine(result);
        }
        catch (Exception e)
        {
            shell.WriteError($"Failed to modify ticket modifier: {e.Message}");
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _playerManager.Sessions.Select(s => s.Name).Distinct().OrderBy(n => n).ToArray();
            return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-scp-modifytickets-hint"));
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(
                ["add", "subtract"],
                Loc.GetString("cmd-scp-modifytickets-hint-operation")
            );
        }

        if (args.Length == 3)
        {
            return CompletionResult.FromHint(Loc.GetString("cmd-scp-modifytickets-hint-amount"));
        }

        return CompletionResult.Empty;
    }
}
