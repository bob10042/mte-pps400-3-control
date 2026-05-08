namespace MtePpsControl.Protocol;

/// <summary>Decoded SE reply: ExxSxxxxx where x is '0' or '1'.</summary>
public sealed record StatusFlags(
    bool Error,
    bool Warning,
    bool DosContactU,
    bool DosContactI,
    bool Busy,
    bool PacketSteering,
    bool RippleControl)
{
    public static StatusFlags Empty { get; } = new(false, false, false, false, false, false, false);

    public static bool TryParse(string body, out StatusFlags f)
    {
        // Format: ExxSxxxxx  -> length 9 (2 error bits + S + 5 status bits, plus 'E' prefix)
        // Bytes: [0]=E, [1]=Error, [2]=Warning, [3]=S, [4]=DOS_U, [5]=DOS_I, [6]=Busy, [7]=Packet, [8]=Ripple
        f = Empty;
        if (body.Length < 9 || body[0] != 'E' || body[3] != 'S') return false;
        f = new StatusFlags(
            Error:          body[1] == '1',
            Warning:        body[2] == '1',
            DosContactU:    body[4] == '1',
            DosContactI:    body[5] == '1',
            Busy:           body[6] == '1',
            PacketSteering: body[7] == '1',
            RippleControl:  body[8] == '1');
        return true;
    }
}
