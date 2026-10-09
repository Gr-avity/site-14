using Content.Client._SCP.Tickets;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class LobbyCharacterPreviewPanel
{
    private ClientTicketManager? _ticketManager;
    private Label? _ticketLabel;

    private void InitTickets()
    {
        _ticketLabel = new Label { HorizontalAlignment = HAlignment.Center };
        Loaded.AddChild(_ticketLabel);

        _ticketManager = IoCManager.Resolve<ClientTicketManager>();
        _ticketManager.TicketInfoUpdated += OnTicketInfoUpdated;
        UpdateTicketLabel();
    }

    private void OnTicketInfoUpdated() => UpdateTicketLabel();

    private void UpdateTicketLabel()
    {
        if (_ticketLabel != null && _ticketManager != null)
            _ticketLabel.Text = Loc.GetString("scp-tickets-lobby-balance",
                ("balance", _ticketManager.Balance),
                ("modifier", Math.Round((double)_ticketManager.Modifier, 2)));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _ticketManager != null)
            _ticketManager.TicketInfoUpdated -= OnTicketInfoUpdated;
    }
}
