using System.Collections.ObjectModel;
using MtePpsControl.Protocol;

namespace MtePpsControl.ViewModels;

public sealed class HarmonicsViewModel : ObservableObject
{
    private readonly Func<PpsClient?> _getClient;
    private readonly Func<bool> _isConnected;

    public HarmonicsViewModel(Func<PpsClient?> getClient, Func<bool> isConnected)
    {
        _getClient = getClient;
        _isConnected = isConnected;

        // Harmonics 2..15 — 14 rows is plenty for a working editor; spec allows 2..31.
        Harmonics = new ObservableCollection<HarmonicEntry>(
            Enumerable.Range(2, 14).Select(i => new HarmonicEntry(i)));

        SelectPhase1Command = new RelayCommand(() => SelectedPhase = 1);
        SelectPhase2Command = new RelayCommand(() => SelectedPhase = 2);
        SelectPhase3Command = new RelayCommand(() => SelectedPhase = 3);
        SelectVoltageCommand = new RelayCommand(() => IsVoltage = true);
        SelectCurrentCommand = new RelayCommand(() => IsVoltage = false);

        PushCommand  = new AsyncRelayCommand(PushAsync,  () => _isConnected());
        ClearCommand = new AsyncRelayCommand(ClearAsync, () => _isConnected());
    }

    public ObservableCollection<HarmonicEntry> Harmonics { get; }

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
        set { if (Set(ref _isVoltage, value)) Raise(nameof(IsCurrent)); }
    }
    public bool IsCurrent => !IsVoltage;

    public RelayCommand SelectPhase1Command { get; }
    public RelayCommand SelectPhase2Command { get; }
    public RelayCommand SelectPhase3Command { get; }
    public RelayCommand SelectVoltageCommand { get; }
    public RelayCommand SelectCurrentCommand { get; }
    public AsyncRelayCommand PushCommand { get; }
    public AsyncRelayCommand ClearCommand { get; }

    private async Task PushAsync()
    {
        var client = _getClient();
        if (client is null) return;
        foreach (var h in Harmonics.Where(h => h.Enabled))
        {
            if (IsVoltage)
                await client.SetVoltageHarmonicAsync(SelectedPhase, h.Index, h.AmplitudePercent, h.PhaseDegrees);
            else
                await client.SetCurrentHarmonicAsync(SelectedPhase, h.Index, h.AmplitudePercent, h.PhaseDegrees);
        }
        // Don't auto-SET — user has to press SET on the source-control view to apply.
    }

    private async Task ClearAsync()
    {
        var client = _getClient();
        if (client is null) return;
        if (IsVoltage)
            await client.ClearVoltageHarmonicsAsync(SelectedPhase);
        else
            await client.ClearCurrentHarmonicsAsync(SelectedPhase);
        foreach (var h in Harmonics) { h.Enabled = false; h.AmplitudePercent = 0; h.PhaseDegrees = 0; }
    }

    public void RaiseCanExecute()
    {
        PushCommand.RaiseCanExecuteChanged();
        ClearCommand.RaiseCanExecuteChanged();
    }
}
