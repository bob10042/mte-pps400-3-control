using System.Collections.ObjectModel;
using System.Windows;
using MtePpsControl.Protocol;

namespace MtePpsControl.ViewModels;

public sealed class SequenceViewModel : ObservableObject
{
    private readonly Func<PpsClient?> _getClient;
    private readonly Func<bool> _isConnected;
    private readonly MainViewModel _main;
    private CancellationTokenSource? _runCts;

    public SequenceViewModel(Func<PpsClient?> getClient, Func<bool> isConnected, MainViewModel main)
    {
        _getClient   = getClient;
        _isConnected = isConnected;
        _main        = main;

        Steps = new ObservableCollection<SequenceStep>();

        CaptureCommand = new RelayCommand(Capture);
        RemoveCommand  = new RelayCommand(() => { if (SelectedStep != null) Steps.Remove(SelectedStep); },
                                          () => SelectedStep != null);
        ClearCommand   = new RelayCommand(() => Steps.Clear(), () => Steps.Count > 0);
        MoveUpCommand  = new RelayCommand(MoveUp,  () => SelectedStep != null && Steps.IndexOf(SelectedStep) > 0);
        MoveDownCommand= new RelayCommand(MoveDown,() => SelectedStep != null && Steps.IndexOf(SelectedStep) < Steps.Count - 1);

        RunCommand  = new AsyncRelayCommand(RunAsync,  () => _isConnected() && Steps.Count > 0 && !IsRunning);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsRunning);
    }

    public ObservableCollection<SequenceStep> Steps { get; }

    private SequenceStep? _selectedStep;
    public SequenceStep? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (Set(ref _selectedStep, value))
            {
                RemoveCommand.RaiseCanExecuteChanged();
                MoveUpCommand.RaiseCanExecuteChanged();
                MoveDownCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (Set(ref _isRunning, value))
            {
                RunCommand.RaiseCanExecuteChanged();
                StopCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private int _currentIndex = -1;
    public int CurrentIndex { get => _currentIndex; private set => Set(ref _currentIndex, value); }

    private string _statusLine = "Idle";
    public string StatusLine { get => _statusLine; private set => Set(ref _statusLine, value); }

    public RelayCommand CaptureCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public AsyncRelayCommand RunCommand { get; }
    public AsyncRelayCommand StopCommand { get; }

    private void Capture()
    {
        var step = new SequenceStep
        {
            Name = $"Step {Steps.Count + 1}",
            Frequency = _main.Frequency,
            HoldSeconds = 5,
            U  = _main.Phases.Select(p => p.VoltageSet).ToArray(),
            I  = _main.Phases.Select(p => p.CurrentSet).ToArray(),
            PH = _main.Phases.Select(p => p.VoltagePhaseAngle).ToArray(),
            Phi= _main.Phases.Select(p => p.Phi).ToArray(),
        };
        Steps.Add(step);
        ClearCommand.RaiseCanExecuteChanged();
        RunCommand.RaiseCanExecuteChanged();
    }

    private void MoveUp()
    {
        if (SelectedStep is null) return;
        var i = Steps.IndexOf(SelectedStep);
        if (i <= 0) return;
        Steps.Move(i, i - 1);
    }

    private void MoveDown()
    {
        if (SelectedStep is null) return;
        var i = Steps.IndexOf(SelectedStep);
        if (i < 0 || i >= Steps.Count - 1) return;
        Steps.Move(i, i + 1);
    }

    private async Task RunAsync()
    {
        var c = _getClient();
        if (c is null) return;
        IsRunning = true;
        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;
        try
        {
            for (int i = 0; i < Steps.Count && !ct.IsCancellationRequested; i++)
            {
                CurrentIndex = i;
                var s = Steps[i];
                StatusLine = $"Step {i + 1}/{Steps.Count}: {s.Name} — applying";

                for (int p = 1; p <= 3; p++)
                {
                    await c.SetVoltageAsync(p, s.U[p - 1], ct);
                    await c.SetCurrentAsync(p, s.I[p - 1], ct);
                    await c.SetVoltagePhaseAsync(p, s.PH[p - 1], ct);
                    await c.SetPhiAsync(p, s.Phi[p - 1], ct);
                }
                await c.SetFrequencyAsync(s.Frequency, ct);
                await c.ApplyAsync(ct);

                StatusLine = $"Step {i + 1}/{Steps.Count}: {s.Name} — holding {s.HoldSeconds:F0}s";
                await Task.Delay(TimeSpan.FromSeconds(s.HoldSeconds), ct);
            }
            StatusLine = ct.IsCancellationRequested ? "Aborted" : "Done";
        }
        catch (OperationCanceledException) { StatusLine = "Aborted"; }
        catch (Exception ex) { StatusLine = $"Error: {ex.Message}"; }
        finally
        {
            CurrentIndex = -1;
            IsRunning = false;
            // Safety: ramp off after sequence
            try { if (c.IsOpen) await c.OffAsync(); } catch { }
        }
    }

    private async Task StopAsync()
    {
        _runCts?.Cancel();
        await Task.CompletedTask;
    }

    public void RaiseCanExecute()
    {
        RunCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }
}
