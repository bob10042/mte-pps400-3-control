namespace MtePpsControl.ViewModels;

public sealed class HarmonicEntry : ObservableObject
{
    public HarmonicEntry(int index) => Index = index;

    public int Index { get; }
    public string Label => $"H{Index}";

    private bool _enabled;
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    private double _amplitudePercent;
    public double AmplitudePercent { get => _amplitudePercent; set => Set(ref _amplitudePercent, value); }

    private double _phaseDegrees;
    public double PhaseDegrees { get => _phaseDegrees; set => Set(ref _phaseDegrees, value); }
}
