using Content.Shared._SCP.Tickets;
using Robust.Shared.Network;
using Robust.Client;

namespace Content.Client._SCP.Tickets;

public sealed partial class ClientTicketManager
{
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IBaseClient _client = default!;

    public int Balance { get; private set; }
    public float Modifier { get; private set; } = 1.0f;

    /// <summary>
    /// Raised when the server sends updated ticket info.
    /// </summary>
    public event Action? TicketInfoUpdated;

    /// <summary>
    /// Called once from EntryPoint.Init — before any connection happens.
    /// </summary>
    public void Initialize()
    {
        _net.RegisterNetMessage<MsgTicketInfo>(RxTicketInfo);
        _client.RunLevelChanged += (_, e) =>
        {
            if (e.NewLevel == ClientRunLevel.Initialize)
                Reset();
        };
    }

    public void Reset()
    {
        Balance = 0;
        Modifier = 1.0f;
    }

    private void RxTicketInfo(MsgTicketInfo msg)
    {
        Balance = msg.Balance;
        Modifier = msg.Modifier;
        TicketInfoUpdated?.Invoke();
    }
}
