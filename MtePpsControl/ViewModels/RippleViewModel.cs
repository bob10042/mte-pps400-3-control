using System.Collections.ObjectModel;
using MtePpsControl.Protocol;

namespace MtePpsControl.ViewModels;

public sealed class RippleViewModel : ObservableObject
{
    private readonly Func<PpsClient?> _getClient;
    private readonly Func<bool> _isConnected;

    public RippleViewModel(Func<PpsClient?> getClient, Func<bool> isConnected)
    {
        _getClient = getClient;
        _isConnected = isConnected;

        // Seed with the spec example: S010101 telegram (start + 6 pulse/pause pairs)
        Steps = new ObservableCollection<RippleStep>
        {
            new() { DurationMs = 600, Amplitude = 10, FrequencyHz = 166.7 }, // start pulse
            new() { DurationMs = 200, Amplitude = 0,  FrequencyHz = 166.7 }, // pause
            new() { DurationMs = 200, Amplitude = 10, FrequencyHz = 166.7 }, // pulse
            new() { DurationMs = 200, Amplitude = 0,  FrequencyHz = 166.7 },
        };

        SelectPhase1Command = new RelayCommand(() => SelectedPhase = 1);
        SelectPhase2Command = new RelayCommand(() => SelectedPhase = 2);
        SelectPhase3Command = new RelayCommand(() => SelectedPhase = 3);
        SelectVoltageCommand = new RelayCommand(() => IsVoltage = true);
        SelectCurrentCommand = new RelayCommand(() => IsVoltage = false);
        SelectAbsoluteCommand = new RelayCommand(() => IsPercent = false);
        SelectPercentCommand  = new RelayCommand(() => IsPercent = true);

        AddStepCommand    = new RelayCommand(() => Steps.Add(new RippleStep()));
        DuplicateLastCommand = new RelayCommand(() => { if (Steps.Count > 0) Steps.Add(Steps[^1].Clone()); });
        RemoveStepCommand = new RelayCommand(() => { if (Steps.Count > 0) Steps.RemoveAt(Steps.Count - 1); },
                                              () => Steps.Count > 0);
        ClearStepsCommand = new RelayCommand(() => Steps.Clear());

        PushCommand = new AsyncRelayCommand(PushAsync, () => _isConnected() && Steps.Count > 0);
        StopCommand = new AsyncRelayCommand(StopAsync, () => _isConnected());
    }

    public ObservableCollection<RippleStep> Steps { get; }

    private int _selectedPhase = 1;
    public int SelectedPhase
    {
        get => _selectedPhase;
        set { if (Set(ref _selectedPhase, value)) { Raise(nameof(IsPhase1)); Raise(nameof(IsPhase2)); Raise(nameof(IsPhase3)); } }
    }
    public bool IsPhase1 => SelectedPhase == 1;
    public bool IsPhase2 => SelectedPhase == 2;
    public bool IsPhase3 => SelectedPhase == 3;

    private bool _isVoltage = true;
    public bool IsVoltage
    {
        get => _isVoltage;
        set { if (Set(ref _isVoltage, value)) { Raise(nameof(IsCurrent)); Raise(nameof(AmplitudeUnit)); } }
    }
    public bool IsCurrent => !IsVoltage;

    private bool _isPercent;
    public bool IsPercent
    {
        get => _isPercent;
        set { if (Set(ref _isPercent, value)) { Raise(nameof(IsAbsolute)); Raise(nameof(AmplitudeUnit)); } }
    }
    public bool IsAbsolute => !IsPercent;

    public string AmplitudeUnit => IsPercent ? "%" : (IsVoltage ? "V" : "A");

    public RelayCommand SelectPhase1Command { get; }
    public RelayCommand SelectPhase2Command { get; }
    public RelayCommand SelectPhase3Command { get; }
    public RelayCommand SelectVoltageCommand { get; }
    public RelayCommand SelectCurrentCommand { get; }
    public RelayCommand SelectAbsoluteCommand { get; }
    public RelayCommand SelectPercentCommand { get; }
    public RelayCommand AddStepCommand { get; }
    public RelayCommand DuplicateLastCommand { get; }
    public RelayCommand RemoveStepCommand { get; }
    public RelayCommand ClearStepsCommand { get; }
    public AsyncRelayCommand PushCommand { get; }
    public AsyncRelayCommand StopCommand { get; }

    private async Task PushAsync()
    {
        var c = _getClient();
        if (c is null) return;
        // First clear any existing telegram on this side+phase, then push fresh table.
        if (IsVoltage) await c.StopVoltageRippleAsync(SelectedPhase);
        else           await c.StopCurrentRippleAsync(SelectedPhase);

        foreach (var s in Steps)
        {
            if (IsVoltage)
            {
                if (IsPercent)
                    await c.AddVoltageRipplePulsePercentAsync(SelectedPhase, s.DurationMs, s.Amplitude, s.FrequencyHz);
                else
                    await c.AddVoltageRipplePulseAsync(SelectedPhase, s.DurationMs, s.Amplitude, s.FrequencyHz);
            }
            else
            {
                if (IsPercent)
                    await c.AddCurrentRipplePulsePercentAsync(SelectedPhase, s.DurationMs, s.Amplitude, s.FrequencyHz);
                else
                    await c.AddCurrentRipplePulseAsync(SelectedPhase, s.DurationMs, s.Amplitude, s.FrequencyHz);
            }
        }
        // Per spec: SET kicks off the run when no value was changed since last apply.
        // We DON'T call SET here automatically — keep that explicit on the source view.
    }

    private async Task StopAsync()
    {
        var c = _getClient();
        if (c is null) return;
        if (IsVoltage) await c.StopVoltageRippleAsync(SelectedPhase);
        else           await c.StopCurrentRippleAsync(SelectedPhase);
    }

    public void RaiseCanExecute()
    {
        PushCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }
}
