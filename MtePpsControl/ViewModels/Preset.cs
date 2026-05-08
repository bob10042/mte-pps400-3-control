namespace MtePpsControl.ViewModels;

public sealed class Preset
{
    public string Name { get; set; } = "Preset";
    public DateTime SavedAt { get; set; } = DateTime.Now;

    // Source-control state
    public double Frequency { get; set; } = 50;
    public double VoltageRamp { get; set; } = 0.5;
    public double CurrentRamp { get; set; } = 0.5;
    public double[] U { get; set; } = new double[3];
    public double[] I { get; set; } = new double[3];
    public double[] PH { get; set; } = new[] { 0.0, 120.0, 240.0 };
    public double[] Phi { get; set; } = new double[3];
    public int[] URange { get; set; } = new int[3];
    public int[] IRange { get; set; } = new int[3];
    public bool[] BigCurrent { get; set; } = new bool[3];

    // Harmonics state from the editor (currently-selected phase + side; the user can re-target on load)
    public int   HarmonicPhase { get; set; } = 1;
    public bool  HarmonicIsVoltage { get; set; } = true;
    public List<HarmonicSnapshot> Harmonics { get; set; } = new();

    // Ripple control state from the editor
    public int   RipplePhase { get; set; } = 1;
    public bool  RippleIsVoltage { get; set; } = true;
    public bool  RippleIsPercent { get; set; }
    public List<RippleStepSnapshot> Ripple { get; set; } = new();
}

public sealed class HarmonicSnapshot
{
    public int Index { get; set; }
    public bool Enabled { get; set; }
    public double AmplitudePercent { get; set; }
    public double PhaseDegrees { get; set; }
}

public sealed class RippleStepSnapshot
{
    public double DurationMs { get; set; }
    public double Amplitude { get; set; }
    public double FrequencyHz { get; set; }
}
