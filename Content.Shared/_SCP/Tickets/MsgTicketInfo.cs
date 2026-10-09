using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._SCP.Tickets;

/// <summary>
/// Sent server -> client to inform the client of their ticket balance and modifier.
/// </summary>
public sealed class MsgTicketInfo : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public int Balance { get; set; }
    public float Modifier { get; set; }

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Balance = buffer.ReadInt32();
        Modifier = buffer.ReadFloat();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Balance);
        buffer.Write(Modifier);
    }
}
