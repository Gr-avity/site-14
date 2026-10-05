# ModifyTicketsCommand
cmd-scp-modifytickets-help = Изменить модификатор билетов для игрока
cmd-scp-modifytickets-error-usage = Использование: scp_modifytickets <игрок> <add|subtract> <amount>
cmd-scp-modifytickets-error-invalid-amount = Недопустимая сумма
cmd-scp-modifytickets-error-player-not-found = Игрок '{$player}' не найден
cmd-scp-modifytickets-error-invalid-operation = Недопустимая операция
cmd-scp-modifytickets-result = Модификатор билетов для {$player} изменен с {$old} на {$new}
cmd-scp-modifytickets-hint = <имя игрока>
cmd-scp-modifytickets-hint-operation = add или subtract
cmd-scp-modifytickets-hint-amount = amount

# MassModifyTicketsCommand
cmd-scp-massmodifytickets-help = Добавить билеты всем подключенным игрокам
cmd-scp-massmodifytickets-error-usage = Использование: scp_massmodifytickets <amount>
cmd-scp-massmodifytickets-error-positive = Сумма должна быть положительным числом
cmd-scp-massmodifytickets-announcement = Массово модифицированы билеты для {$count} игроков: +{$amount} каждый
cmd-scp-massmodifytickets-result = {$count} игроков модифицированы с +{$amount} билетами

# ModifyTicketBalanceCommand
cmd-scp-modifyticketbalance-help = Изменить баланс билетов игрока
cmd-scp-modifyticketbalance-error-usage = Использование: scp_modifyticketbalance <игрок> <add|subtract|set> <amount>
cmd-scp-modifyticketbalance-error-invalid-amount = Недопустимая сумма
cmd-scp-modifyticketbalance-error-player-not-found = Игрок '{$player}' не найден
cmd-scp-modifyticketbalance-error-invalid-operation = Недопустимая операция
cmd-scp-modifyticketbalance-result = Баланс билетов для {$player} изменен с {$old} на {$new}
cmd-scp-modifyticketbalance-hint = <имя игрока>
cmd-scp-modifyticketbalance-hint-operation = add, subtract или set
cmd-scp-modifyticketbalance-hint-amount = amount