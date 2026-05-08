using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MtePpsControl.ViewModels;

public sealed class DatabaseViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly string _storePath;

    public DatabaseViewModel(MainViewModel main)
    {
        _main = main;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MtePpsControl");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "presets.json");

        Presets = new ObservableCollection<Preset>(LoadAll());

        SaveCommand   = new RelayCommand(Save,   () => !string.IsNullOrWhiteSpace(NewPresetName));
        LoadCommand   = new RelayCommand(Load,   () => SelectedPreset != null);
        DeleteCommand = new RelayCommand(Delete, () => SelectedPreset != null);
    }

    public ObservableCollection<Preset> Presets { get; }

    private string _newPresetName = "";
    public string NewPresetName
    {
        get => _newPresetName;
        set { if (Set(ref _newPresetName, value)) SaveCommand.RaiseCanExecuteChanged(); }
    }

    private Preset? _selectedPreset;
    public Preset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (Set(ref _selectedPreset, value))
            {
                LoadCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand LoadCommand { get; }
    public RelayCommand DeleteCommand { get; }

    private void Save()
    {
        var p = new Preset
        {
            Name = NewPresetName.Trim(),
            SavedAt = DateTime.Now,
            Frequency = _main.Frequency,
            VoltageRamp = _main.VoltageRamp,
            CurrentRamp = _main.CurrentRamp,
            U   = _main.Phases.Select(x => x.VoltageSet).ToArray(),
            I   = _main.Phases.Select(x => x.CurrentSet).ToArray(),
            PH  = _main.Phases.Select(x => x.VoltagePhaseAngle).ToArray(),
            Phi = _main.Phases.Select(x => x.Phi).ToArray(),
            URange = _main.Phases.Select(x => x.VoltageRange).ToArray(),
            IRange = _main.Phases.Select(x => x.CurrentRange).ToArray(),
            BigCurrent = _main.Phases.Select(x => x.BigCurrentConnector).ToArray(),

            HarmonicPhase     = _main.Harmonics.SelectedPhase,
            HarmonicIsVoltage = _main.Harmonics.IsVoltage,
            Harmonics = _main.Harmonics.Harmonics.Select(h => new HarmonicSnapshot
            {
                Index = h.Index, Enabled = h.Enabled,
                AmplitudePercent = h.AmplitudePercent, PhaseDegrees = h.PhaseDegrees
            }).ToList(),

            RipplePhase     = _main.Ripple.SelectedPhase,
            RippleIsVoltage = _main.Ripple.IsVoltage,
            RippleIsPercent = _main.Ripple.IsPercent,
            Ripple = _main.Ripple.Steps.Select(s => new RippleStepSnapshot
            {
                DurationMs = s.DurationMs, Amplitude = s.Amplitude, FrequencyHz = s.FrequencyHz
            }).ToList(),
        };
        // Replace if name exists
        var existing = Presets.FirstOrDefault(x => x.Name == p.Name);
        if (existing != null) Presets.Remove(existing);
        Presets.Add(p);
        Persist();
        NewPresetName = "";
    }

    private void Load()
    {
        if (SelectedPreset is null) return;
        var p = SelectedPreset;
        _main.Frequency   = p.Frequency;
        _main.VoltageRamp = p.VoltageRamp;
        _main.CurrentRamp = p.CurrentRamp;
        for (int i = 0; i < 3; i++)
        {
            _main.Phases[i].VoltageSet         = p.U[i];
            _main.Phases[i].CurrentSet         = p.I[i];
            _main.Phases[i].VoltagePhaseAngle  = p.PH[i];
            _main.Phases[i].Phi                = p.Phi[i];
            _main.Phases[i].VoltageRange       = p.URange[i];
            _main.Phases[i].CurrentRange       = p.IRange[i];
            _main.Phases[i].BigCurrentConnector= p.BigCurrent[i];
        }

        // Restore harmonics editor state
        _main.Harmonics.SelectedPhase = p.HarmonicPhase;
        _main.Harmonics.IsVoltage     = p.HarmonicIsVoltage;
        // Match snapshots to existing rows by index; create no extras (rows are fixed 2..15)
        foreach (var h in _main.Harmonics.Harmonics)
        {
            var snap = p.Harmonics.FirstOrDefault(s => s.Index == h.Index);
            if (snap is null)
            {
                h.Enabled = false; h.AmplitudePercent = 0; h.PhaseDegrees = 0;
            }
            else
            {
                h.Enabled = snap.Enabled;
                h.AmplitudePercent = snap.AmplitudePercent;
                h.PhaseDegrees = snap.PhaseDegrees;
            }
        }

        // Restore ripple telegram editor state
        _main.Ripple.SelectedPhase = p.RipplePhase;
        _main.Ripple.IsVoltage     = p.RippleIsVoltage;
        _main.Ripple.IsPercent     = p.RippleIsPercent;
        _main.Ripple.Steps.Clear();
        foreach (var rs in p.Ripple)
            _main.Ripple.Steps.Add(new RippleStep { DurationMs = rs.DurationMs, Amplitude = rs.Amplitude, FrequencyHz = rs.FrequencyHz });

        // Switch to Source view so user sees the loaded values
        _main.SelectedView = _main.SourceView;
    }

    private void Delete()
    {
        if (SelectedPreset is null) return;
        Presets.Remove(SelectedPreset);
        Persist();
    }

    private IEnumerable<Preset> LoadAll()
    {
        try
        {
            if (!File.Exists(_storePath)) return Array.Empty<Preset>();
            var json = File.ReadAllText(_storePath);
            return JsonSerializer.Deserialize<List<Preset>>(json) ?? new List<Preset>();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load presets: {ex.Message}", "MTE PPS Control");
            return Array.Empty<Preset>();
        }
    }

    private void Persist()
    {
        try
        {
            var json = JsonSerializer.Serialize(Presets.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storePath, json);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save presets: {ex.Message}", "MTE PPS Control");
        }
    }
}
