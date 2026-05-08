namespace MtePpsControl.Protocol;

/// <summary>Decoded value of STATU&lt;Phase&gt; / STATI&lt;Phase&gt;.</summary>
public enum PhaseStatus
{
    Off       = 0,
    On        = 1,
    Overload  = 2,   // also: idle when no SET issued yet
    ErrorOff  = 3,   // DSP switched off due to overload — needs OFF or new SET
    DisOff    = 4,   // DSP switched off by DIS — needs OFF or new SET
    Unknown   = -1,
}
