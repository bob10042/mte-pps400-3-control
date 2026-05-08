using MtePpsControl.Protocol;

namespace MtePpsControl.ViewModels;

/// <summary>One row of the source-control grid: U/I/PH/W setpoints + measured-back values for one phase.</summary>
public sealed class PhaseSetpointViewModel : ObservableObject
{
    public PhaseSetpointViewModel(int phase)
    {
        Phase = phase;
        // Default to standard 3-phase angles
        VoltagePhaseAngle = phase switch { 1 => 0, 2 => 120, _ => 240 };
    }

    public int Phase { get; }
    public string Label => $"Phase {Phase}";

    // IEC 60446 / common UK convention — keep distinguishable + colour-blind safe enough
    public string PhaseAccentColor => Phase switch
    {
        1 => "#E53935", // red   (L1)
        2 => "#FFB300", // amber (L2)
        3 => "#3FA9F5", // blue  (L3)
        _ => "#888888"
    };

    private double _voltageSet;
    public double VoltageSet { get => _voltageSet; set => Set(ref _voltageSet, value); }

    private double _currentSet;
    public double CurrentSet { get => _currentSet; set => Set(ref _currentSet, value); }

    private double _voltagePhaseAngle;
    public double VoltagePhaseAngle { get => _voltagePhaseAngle; set => Set(ref _voltagePhaseAngle, value); }

    private double _phi;
    public double Phi { get => _phi; set => Set(ref _phi, value); }

    private double _voltageMeasured;
    public double VoltageMeasured
    {
        get => _voltageMeasured;
        set { if (Set(ref _voltageMeasured, value)) RaiseDerived(); }
    }

    private double _currentMeasured;
    public double CurrentMeasured
    {
        get => _currentMeasured;
        set { if (Set(ref _currentMeasured, value)) RaiseDerived(); }
    }

    private string _statusU = "—";
    public string StatusU
    {
        get => _statusU;
        set { if (Set(ref _statusU, value)) { Raise(nameof(StatusUColor)); Raise(nameof(StatusULabel)); } }
    }

    private string _statusI = "—";
    public string StatusI
    {
        get => _statusI;
        set { if (Set(ref _statusI, value)) { Raise(nameof(StatusIColor)); Raise(nameof(StatusILabel)); } }
    }

    public string StatusUColor => ColorForStatus(_statusU);
    public string StatusIColor => ColorForStatus(_statusI);
    public string StatusULabel => LabelForStatus(_statusU);
    public string StatusILabel => LabelForStatus(_statusI);

    private static string ColorForStatus(string s) => s switch
    {
        "Off"      => "#888888",   // grey
        "On"       => "#43A047",   // green
        "Overload" => "#FFB300",   // amber (also: idle-no-SET)
        "ErrorOff" => "#E53935",   // red
        "DisOff"   => "#E53935",
        _ => "#555555"
    };
    private static string LabelForStatus(string s) => s switch
    {
        "Off"      => "OFF",
        "On"       => "ON",
        "Overload" => "OVL",
        "ErrorOff" => "ERR",
        "DisOff"   => "DIS",
        _ => "—"
    };

    // BU<n>,<r> / BI<n>,<r> — 0 = auto/smallest possible range
    private int _voltageRange;
    public int VoltageRange { get => _voltageRange; set => Set(ref _voltageRange, value); }

    private int _currentRange;
    public int CurrentRange { get => _currentRange; set => Set(ref _currentRange, value); }

    // SKLI<phase>,<flag> — big/small current connector relay (PPS 400.3 only)
    private bool _bigCurrentConnector;
    public bool BigCurrentConnector { get => _bigCurrentConnector; set => Set(ref _bigCurrentConnector, value); }

    // Live measurement readouts (?<n>)
    private double _thdU;
    public double ThdU { get => _thdU; set => Set(ref _thdU, value); }

    private double _thdI;
    public double ThdI { get => _thdI; set => Set(ref _thdI, value); }

    private double _phiU;
    public double PhiU { get => _phiU; set => Set(ref _phiU, value); }

    private double _phiI;
    public double PhiI
    {
        get => _phiI;
        set { if (Set(ref _phiI, value)) RaiseDerived(); }
    }

    // ---- Derived per-phase power values (computed from measured U, I, φ) ----
    public double ActivePower    => MtePpsControl.Protocol.PpsCalculations.ActivePower(_voltageMeasured, _currentMeasured, _phiI);
    public double ReactivePower  => MtePpsControl.Protocol.PpsCalculations.ReactivePower(_voltageMeasured, _currentMeasured, _phiI);
    public double ApparentPower  => MtePpsControl.Protocol.PpsCalculations.ApparentPower(_voltageMeasured, _currentMeasured);
    public double PowerFactor    => MtePpsControl.Protocol.PpsCalculations.PowerFactor(_phiI);

    private void RaiseDerived()
    {
        Raise(nameof(ActivePower));
        Raise(nameof(ReactivePower));
        Raise(nameof(ApparentPower));
        Raise(nameof(PowerFactor));
    }
}
