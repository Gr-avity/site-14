using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Server._SCP.Tickets.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed partial class ModifyTicketBalanceCommand : LocalizedEntityCommands
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IPlayerLocator _locator = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override string Command => "scp_modifyticketbalance";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        try
        {
            if (args.Length < 3)
            {
                shell.WriteError(Loc.GetString("cmd-scp-modifyticketbalance-error-usage"));
                return;
            }

            var targetArg = args[0];
            var operation = args[1].ToLowerInvariant();
            var amountStr = args[2];

            if (operation != "add" && operation != "subtract" && operation != "set")
            {
                shell.WriteError(Loc.GetString("cmd-scp-modifyticketbalance-error-invalid-operation"));
                return;
            }

            if (!int.TryParse(amountStr, out var amount) || amount < 0)
            {
                shell.WriteError(Loc.GetString("cmd-scp-modifyticketbalance-error-invalid-amount"));
                return;
            }

            var data = await _locator.LookupIdByNameOrIdAsync(targetArg);
            if (data == null)
            {
                shell.WriteError(Loc.GetString("cmd-scp-modifyticketbalance-error-player-not-found", ("player", targetArg)));
                return;
            }

            var userId = new NetUserId(data.UserId);

            var system = EntityManager.System<TicketSystem>();
            var (currentBalance, currentModifier) = await system.GetTicketsAsync(userId);

            int newBalance;
            if (operation == "add")
            {
                if (amount > int.MaxValue - currentBalance)
                {
                    shell.WriteError(Loc.GetString("cmd-scp-modifyticketbalance-error-overflow"));
                    return;
                }
                newBalance = currentBalance + amount;
            }
            else if (operation == "subtract")
            {
                newBalance = Math.Max(0, currentBalance - amount);
            }
            else // set
            {
                newBalance = amount;
            }

            await system.SetTicketBalanceAsync(userId, newBalance);

            _adminLogger.Add(
                LogType.AdminCommands,
                LogImpact.Medium,
                $"{shell.Player} modified ticket balance for {data.Username}: {currentBalance} -> {newBalance}"
            );

            var result = Loc.GetString(
                "cmd-scp-modifyticketbalance-result",
                ("player", data.Username),
                ("old", currentBalance.ToString()),
                ("new", newBalance.ToString())
            );
            shell.WriteLine(result);
        }
        catch (Exception e)
        {
            shell.WriteError($"Failed to modify ticket balance: {e.Message}");
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _playerManager.Sessions.Select(s => s.Name).Distinct().OrderBy(n => n).ToArray();
            return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-scp-modifyticketbalance-hint"));
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(
                ["add", "subtract", "set"],
                Loc.GetString("cmd-scp-modifyticketbalance-hint-operation")
            );
        }

        if (args.Length == 3)
        {
            return CompletionResult.FromHint(Loc.GetString("cmd-scp-modifyticketbalance-hint-amount"));
        }

        return CompletionResult.Empty;
    }
}
