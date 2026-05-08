namespace MtePpsControl.Protocol;

/// <summary>One logical line of reply from the PPS — a single CR-terminated record.</summary>
public sealed record PpsReply(string Raw)
{
    /// <summary>Left of '=' if MODE=1 framing is present, else the whole line.</summary>
    public string Head => Raw.Contains('=') ? Raw[..Raw.IndexOf('=')] : Raw;

    /// <summary>Right of '=' if MODE=1 framing is present, else the whole line.</summary>
    public string Body => Raw.Contains('=') ? Raw[(Raw.IndexOf('=') + 1)..] : Raw;

    public bool IsOk    => Body == "O";
    public bool IsError => Body == "E";
    public bool IsUnknown => Body == "?";

    public bool TryDouble(out double v) => double.TryParse(Body,
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out v);

    public bool TryInt(out int v) => int.TryParse(Body, out v);
}
