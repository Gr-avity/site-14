using System.Diagnostics.CodeAnalysis;
using Content.Client._SCP.Tickets;
using Content.Shared.Roles;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private ClientTicketManager? _ticketManager;
    private bool _ticketSubscribed;

    /// <summary>
    /// Returns false if the role is TicketOnly and there are insufficient tickets; the reason is output in ticketReason.
    /// </summary>
    private bool CheckJobTickets(JobPrototype job,
        [NotNullWhen(false)] out FormattedMessage? ticketReason)
    {
        ticketReason = null;

        _ticketManager ??= IoCManager.Resolve<ClientTicketManager>();
        if (!_ticketSubscribed)
        {
            _ticketSubscribed = true;
            _ticketManager.TicketInfoUpdated += RefreshJobs;
        }

        if (!job.TicketOnly || job.TicketCost <= 0 || job.TicketCost <= _ticketManager.Balance)
            return true;

        ticketReason = FormattedMessage.FromMarkupOrThrow(Loc.GetString("scp-tickets-insufficient",
            ("role", job.LocalizedName),
            ("cost", job.TicketCost),
            ("balance", _ticketManager.Balance)));
        return false;
    }
}
