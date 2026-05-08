using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Windows;
using MtePpsControl.Protocol;

namespace MtePpsControl.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private PpsClient? _client;
    private CancellationTokenSource? _pollCts;
    private readonly AppSettings _settings;
    private bool _connectInFlight;
    private bool _detectInFlight;

    // last-applied snapshot for dirty detection
    private double[] _appliedU = new double[3];
    private double[] _appliedI = new double[3];
    private double[] _appliedPH = new double[3];
    private double[] _appliedPhi = new double[3];
    private double _appliedFreq;

    public MainViewModel()
    {
        _settings = AppSettings.Load();

        AvailablePorts = new ObservableCollection<string>(SerialPort.GetPortNames().OrderBy(s => s));
        // Prefer last-good > COM5 > first available
        _selectedPort = AvailablePorts.FirstOrDefault(p => p.Equals(_settings.LastPort, StringComparison.OrdinalIgnoreCase))
                     ?? AvailablePorts.FirstOrDefault(p => p.Equals("COM5",            StringComparison.OrdinalIgnoreCase))
                     ?? AvailablePorts.FirstOrDefault();

        Phases = new ObservableCollection<PhaseSetpointViewModel>
        {
            new(1), new(2), new(3),
        };
        foreach (var p in Phases) p.PropertyChanged += OnPhaseChanged;

        ConnectCommand        = new AsyncRelayCommand(ConnectAsync,    () => !IsConnected && SelectedPort != null);
        DisconnectCommand     = new AsyncRelayCommand(DisconnectAsync, () => IsConnected);
        ApplyCommand          = new AsyncRelayCommand(ApplyAsync,      () => IsConnected);
        OffCommand            = new AsyncRelayCommand(OffAsync,        () => IsConnected);
        EmergencyCommand      = new AsyncRelayCommand(EmergencyAsync,  () => IsConnected);
        RefreshPortsCommand   = new RelayCommand(RefreshPorts);
        AutoDetectCommand     = new AsyncRelayCommand(AutoDetectAsync, () => !IsConnected);

        Harmonics = new HarmonicsViewModel(() => _client, () => IsConnected);
        Ripple    = new RippleViewModel   (() => _client, () => IsConnected);
        Sequence  = new SequenceViewModel (() => _client, () => IsConnected, this);
        Database  = new DatabaseViewModel (this);
        OpLog     = new OperationalLog();
        Setter    = new ParameterSetterViewModel(() => _client, () => IsConnected,
                                                 (s, r, st) => OpLog.LogCommand(s, r, st, "Setter"));
        Csv       = new CsvLogger();
        ToggleCsvLogCommand = new RelayCommand(ToggleCsv);

        SelectedView = SetterViewKey; // unified setter is the new default landing

        // Kick off the auto-detect on launch via the UI dispatcher so all property
        // updates land on the UI thread (the actual serial probe still yields via async).
        if (_settings.AutoConnectOnStart)
        {
            // Defer until after the window has finished loading.
            Application.Current?.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await Task.Delay(400); await AutoDetectAsync(); }
                catch { /* startup probe shouldn't break the UI */ }
            }));
        }
    }

    public HarmonicsViewModel       Harmonics { get; }
    public RippleViewModel          Ripple { get; }
    public SequenceViewModel        Sequence { get; }
    public DatabaseViewModel        Database { get; }
    public ParameterSetterViewModel Setter { get; }
    public CsvLogger                Csv { get; }
    public OperationalLog           OpLog { get; }
    public RelayCommand             ToggleCsvLogCommand { get; }
    public bool   IsCsvLogging => Csv.IsLogging;
    public string CsvLogStatus => Csv.IsLogging
        ? $"📝 logging → {System.IO.Path.GetFileName(Csv.CurrentFilePath)}"
        : "log off";

    private void ToggleCsv()
    {
        if (Csv.IsLogging) Csv.Stop();
        else Csv.Start();
        Raise(nameof(IsCsvLogging));
        Raise(nameof(CsvLogStatus));
    }

    // ---- Connection state ----
    public ObservableCollection<string> AvailablePorts { get; }

    private string? _selectedPort;
    public string? SelectedPort
    {
        get => _selectedPort;
        set
        {
            if (Set(ref _selectedPort, value))
            {
                ConnectCommand.RaiseCanExecuteChanged();
                if (!string.IsNullOrEmpty(value))
                {
                    _settings.LastPort = value;
                    _settings.Save();
                }
            }
        }
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (Set(ref _isConnected, value))
            {
                Raise(nameof(ConnectionLabel));
                Raise(nameof(ConnectionStateColor));
                ConnectCommand.RaiseCanExecuteChanged();
                DisconnectCommand.RaiseCanExecuteChanged();
                ApplyCommand.RaiseCanExecuteChanged();
                OffCommand.RaiseCanExecuteChanged();
                EmergencyCommand.RaiseCanExecuteChanged();
                AutoDetectCommand.RaiseCanExecuteChanged();
                Harmonics.RaiseCanExecute();
                Ripple.RaiseCanExecute();
                Sequence.RaiseCanExecute();
                Setter.RaiseCanExecute();
            }
        }
    }
    public string ConnectionLabel => IsConnected
        ? $"● {SelectedPort}  19200 8N1  {(_client?.ModeExtended == true ? "MODE1" : "MODE0")}"
        : "○ disconnected";
    public string ConnectionStateColor => IsConnected ? "#43A047" : "#888888";

    private string _versionLine = "—";
    public string VersionLine { get => _versionLine; set => Set(ref _versionLine, value); }

    private string _autoDetectStatus = "Idle";
    public string AutoDetectStatus { get => _autoDetectStatus; set => Set(ref _autoDetectStatus, value); }

    // ---- Setpoints (whole-source) ----
    public ObservableCollection<PhaseSetpointViewModel> Phases { get; }

    private double _frequency = 50;
    public double Frequency { get => _frequency; set { if (Set(ref _frequency, value)) UpdateDirty(); } }

    private double _voltageRamp = 0.5;
    public double VoltageRamp { get => _voltageRamp; set => Set(ref _voltageRamp, value); }

    private double _currentRamp = 0.5;
    public double CurrentRamp { get => _currentRamp; set => Set(ref _currentRamp, value); }

    // ---- Status footer ----
    private string _seFlags = "—";
    public string SeFlags { get => _seFlags; set => Set(ref _seFlags, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }

    // ---- Smart state: output-on, dirty, errors ----

    /// <summary>True when ANY phase amp is reporting STATU=1 or STATI=1 (DSP energized).</summary>
    public bool IsOutputOn => Phases.Any(p => p.StatusU == "On" || p.StatusI == "On");
    public string OutputOnLabel => IsOutputOn ? "OUTPUT ON" : "OUTPUT OFF";
    public string OutputOnColor => IsOutputOn ? "#FFE53935" : "#FF43A047"; // red when on, green when off

    /// <summary>Setpoints differ from last applied (i.e. SET would change something).</summary>
    public bool HasUncommittedChanges
    {
        get
        {
            for (int i = 0; i < 3; i++)
            {
                if (!Eq(Phases[i].VoltageSet, _appliedU[i])) return true;
                if (!Eq(Phases[i].CurrentSet, _appliedI[i])) return true;
                if (!Eq(Phases[i].VoltagePhaseAngle, _appliedPH[i])) return true;
                if (!Eq(Phases[i].Phi, _appliedPhi[i])) return true;
            }
            return !Eq(Frequency, _appliedFreq);
        }
    }
    public string SetButtonLabel => HasUncommittedChanges ? "SET *" : "SET";

    /// <summary>True when SE flags or per-phase status indicates a problem.</summary>
    public bool HasError => Phases.Any(p => p.StatusU == "ErrorOff" || p.StatusU == "DisOff"
                                          || p.StatusI == "ErrorOff" || p.StatusI == "DisOff");

    private static bool Eq(double a, double b) => Math.Abs(a - b) < 1e-9;

    private void OnPhaseChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PhaseSetpointViewModel.VoltageSet)
                            or nameof(PhaseSetpointViewModel.CurrentSet)
                            or nameof(PhaseSetpointViewModel.VoltagePhaseAngle)
                            or nameof(PhaseSetpointViewModel.Phi))
            UpdateDirty();
        if (e.PropertyName is nameof(PhaseSetpointViewModel.StatusU)
                            or nameof(PhaseSetpointViewModel.StatusI))
        {
            Raise(nameof(IsOutputOn));
            Raise(nameof(OutputOnLabel));
            Raise(nameof(OutputOnColor));
            Raise(nameof(HasError));
        }
    }

    private void UpdateDirty()
    {
        Raise(nameof(HasUncommittedChanges));
        Raise(nameof(SetButtonLabel));
    }

    // ---- Navigation ----
    public string SetterViewKey   => "setter";
    public string SourceView      => "source";
    public string HarmonicsView   => "harmonics";
    public string RippleView      => "ripple";
    public string SequenceView    => "sequence";
    public string DatabaseView    => "database";

    private string _selectedView = "source";
    public string SelectedView { get => _selectedView; set => Set(ref _selectedView, value); }

    public RelayCommand RefreshPortsCommand { get; }
    public AsyncRelayCommand ConnectCommand { get; }
    public AsyncRelayCommand DisconnectCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand OffCommand { get; }
    public AsyncRelayCommand EmergencyCommand { get; }
    public AsyncRelayCommand AutoDetectCommand { get; }

    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var p in SerialPort.GetPortNames().OrderBy(s => s))
            AvailablePorts.Add(p);
        if (SelectedPort != null && !AvailablePorts.Contains(SelectedPort))
            SelectedPort = AvailablePorts.FirstOrDefault();
    }

    /// <summary>Probe every COM port for a PPS and connect to the first hit.</summary>
    private async Task AutoDetectAsync()
    {
        if (IsConnected || _detectInFlight) return;
        _detectInFlight = true;
        AutoDetectStatus = "Scanning ports…";
        try
        {
            var hit = await PpsAutoDetect.ProbeAsync(_settings.LastPort);
            if (hit is null)
            {
                AutoDetectStatus = "No PPS found on any COM port";
                return;
            }
            SelectedPort = hit.PortName;
            AutoDetectStatus = $"Found {hit.PortName} → connecting";
            await ConnectAsync();
            AutoDetectStatus = IsConnected ? $"Auto-connected on {hit.PortName}" : "Auto-connect failed";
        }
        catch (Exception ex)
        {
            AutoDetectStatus = $"Detect error: {ex.Message}";
        }
        finally { _detectInFlight = false; }
    }

    private async Task ConnectAsync()
    {
        if (IsConnected || _connectInFlight || SelectedPort is null) return;
        _connectInFlight = true;
        _client = new PpsClient(SelectedPort);
        try
        {
            _client.Open();
            VersionLine = await _client.VersionAsync();
            await _client.SetExtendedModeAsync();
            IsConnected = true;
            // ConnectionLabel update is best-effort — swallow any UI dispatch quirks.
            try { Raise(nameof(ConnectionLabel)); } catch { }

            _settings.LastPort = SelectedPort;
            _settings.Save();

            _pollCts = new CancellationTokenSource();
            _ = PollLoopAsync(_pollCts.Token);
        }
        catch (Exception ex)
        {
            // If we got far enough to actually connect, the exception is likely a
            // post-connect UI raise quirk (e.g. cross-thread on RaiseCanExecuteChanged) —
            // don't scare the user with a "Connect failed" dialog when the link is good.
            if (!IsConnected)
            {
                if (_client != null) { try { await _client.DisposeAsync(); } catch { } _client = null; }
                Application.Current?.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Connect failed: {ex.Message}", "MTE PPS Control",
                                    MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }
        finally { _connectInFlight = false; }
    }

    private async Task DisconnectAsync()
    {
        if (!IsConnected) return;
        // Safety: ramp off before releasing the port if we left output on.
        try { if (_client != null && IsOutputOn) await _client.OffAsync(OffMode.RampedAll); } catch { }
        _pollCts?.Cancel();
        await Task.Delay(150);
        if (_client != null) await _client.DisposeAsync();
        _client = null;
        IsConnected = false;
        VersionLine = "—";
        SeFlags = "—";
    }

    private async Task ApplyAsync()
    {
        if (_client is null) return;
        foreach (var p in Phases)
        {
            await _client.SetCurrentConnectorRelayAsync(p.Phase, p.BigCurrentConnector ? 1 : 0);
            await _client.SetVoltageRangeAsync(p.Phase, p.VoltageRange);
            await _client.SetCurrentRangeAsync(p.Phase, p.CurrentRange);
            await _client.SetVoltageAsync(p.Phase, p.VoltageSet);
            await _client.SetCurrentAsync(p.Phase, p.CurrentSet);
            await _client.SetVoltagePhaseAsync(p.Phase, p.VoltagePhaseAngle);
            await _client.SetPhiAsync(p.Phase, p.Phi);
            await _client.SetVoltageRampAsync(p.Phase, VoltageRamp);
            await _client.SetCurrentRampAsync(p.Phase, CurrentRamp);
        }
        await _client.SetFrequencyAsync(Frequency);
        await _client.ApplyAsync();

        // Snapshot what we just sent for dirty-detection
        for (int i = 0; i < 3; i++)
        {
            _appliedU[i]   = Phases[i].VoltageSet;
            _appliedI[i]   = Phases[i].CurrentSet;
            _appliedPH[i]  = Phases[i].VoltagePhaseAngle;
            _appliedPhi[i] = Phases[i].Phi;
        }
        _appliedFreq = Frequency;
        UpdateDirty();
    }

    private async Task OffAsync()
    {
        if (_client is null) return;
        await _client.OffAsync(OffMode.RampedAll);
    }

    private async Task EmergencyAsync()
    {
        if (_client is null) return;
        await _client.EmergencyOffAsync();
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        int tick = 0;
        while (!ct.IsCancellationRequested && _client is not null)
        {
            try
            {
                var statU = await _client.ReadVoltagePhaseStatusAsync(ct);
                var statI = await _client.ReadCurrentPhaseStatusAsync(ct);
                var measI = await _client.ReadMeasuredCurrentsAsync(ct);
                var measU = await _client.ReadMeasuredVoltagesAsync(ct);
                var se    = await _client.StatusAsync(ct);
                var busy  = await _client.IsBusyAsync(ct);

                double[]? thdI = null, thdU = null, phiI = null, phiU = null;
                switch (tick % 4)
                {
                    case 0: thdI = await _client.ReadThdCurrentAsync(ct);   break;
                    case 1: thdU = await _client.ReadThdVoltageAsync(ct);   break;
                    case 2: phiI = await _client.ReadPhiCurrentAsync(ct);   break;
                    case 3: phiU = await _client.ReadPhiVoltageAsync(ct);   break;
                }

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    for (int p = 0; p < 3; p++)
                    {
                        Phases[p].StatusU = statU[p].ToString();
                        Phases[p].StatusI = statI[p].ToString();
                        Phases[p].VoltageMeasured = measU[p];
                        Phases[p].CurrentMeasured = measI[p];
                        if (thdI != null) Phases[p].ThdI = thdI[p];
                        if (thdU != null) Phases[p].ThdU = thdU[p];
                        if (phiI != null) Phases[p].PhiI = phiI[p];
                        if (phiU != null) Phases[p].PhiU = phiU[p];
                    }
                    SeFlags = $"E:{(se.Error?1:0)} W:{(se.Warning?1:0)} U:{(se.DosContactU?1:0)} I:{(se.DosContactI?1:0)} B:{(se.Busy?1:0)} P:{(se.PacketSteering?1:0)} R:{(se.RippleControl?1:0)}";
                    IsBusy = busy;

                    // Write a CSV row if logging is on. Snapshot current per-phase state.
                    var thdUs = Phases.Select(p => p.ThdU).ToArray();
                    var thdIs = Phases.Select(p => p.ThdI).ToArray();
                    var phiUs = Phases.Select(p => p.PhiU).ToArray();
                    var phiIs = Phases.Select(p => p.PhiI).ToArray();
                    var statUs= Phases.Select(p => p.StatusU).ToArray();
                    var statIs= Phases.Select(p => p.StatusI).ToArray();

                    if (Csv.IsLogging)
                        Csv.WriteTick(measU, measI, phiUs, phiIs, thdUs, thdIs, statUs, statIs);

                    // SQLite operational log captures every tick, regardless of CSV setting
                    try { OpLog.LogMeasurement(measU, measI, thdUs, thdIs, phiUs, phiIs, statUs, statIs); }
                    catch { /* tolerate transient db errors */ }
                });
            }
            catch (OperationCanceledException) { return; }
            catch { /* tolerate transient I/O errors */ }
            tick++;
            try { await Task.Delay(1000, ct); }
            catch (OperationCanceledException) { return; }
        }
    }
}
