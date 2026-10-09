# ModifyTicketsCommand
cmd-scp-modifytickets-help = Modify ticket modifier for a player
cmd-scp-modifytickets-error-usage = Usage: scp_modifytickets <target> <add|subtract> <amount>
cmd-scp-modifytickets-error-invalid-amount = Invalid amount
cmd-scp-modifytickets-error-player-not-found = Player '{$player}' not found
cmd-scp-modifytickets-error-invalid-operation = Invalid operation
cmd-scp-modifytickets-result = Ticket modifier for {$player} changed from {$old} to {$new}
cmd-scp-modifytickets-hint = <player name>
cmd-scp-modifytickets-hint-operation = add or subtract
cmd-scp-modifytickets-hint-amount = amount

# MassModifyTicketsCommand
cmd-scp-massmodifytickets-help = Add tickets to all connected players
cmd-scp-massmodifytickets-error-usage = Usage: scp_massmodifytickets <amount>
cmd-scp-massmodifytickets-error-positive = Amount must be a positive number
cmd-scp-massmodifytickets-announcement = Mass modified tickets for {$count} players: +{$amount} each
cmd-scp-massmodifytickets-result = {$count} players modified with +{$amount} tickets each

# ModifyTicketBalanceCommand
cmd-scp-modifyticketbalance-help = Modify ticket balance for a player
cmd-scp-modifyticketbalance-error-usage = Usage: scp_modifyticketbalance <target> <add|subtract|set> <amount>
cmd-scp-modifyticketbalance-error-invalid-amount = Invalid amount
cmd-scp-modifyticketbalance-error-player-not-found = Player '{$player}' not found
cmd-scp-modifyticketbalance-error-invalid-operation = Invalid operation
cmd-scp-modifyticketbalance-result = Ticket balance for {$player} changed from {$old} to {$new}
cmd-scp-modifyticketbalance-hint = <player name>
cmd-scp-modifyticketbalance-hint-operation = add, subtract or set
cmd-scp-modifyticketbalance-hint-amount = amount