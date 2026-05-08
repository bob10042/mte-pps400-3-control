namespace MtePpsControl.ViewModels;

public sealed class RippleStep : ObservableObject
{
    private double _durationMs = 200;
    public double DurationMs { get => _durationMs; set => Set(ref _durationMs, value); }

    private double _amplitude;
    public double Amplitude { get => _amplitude; set => Set(ref _amplitude, value); }

    private double _frequencyHz = 166.7;
    public double FrequencyHz { get => _frequencyHz; set => Set(ref _frequencyHz, value); }

    public bool IsPulse => Amplitude > 0;

    public RippleStep Clone() => new() { DurationMs = DurationMs, Amplitude = Amplitude, FrequencyHz = FrequencyHz };
}
