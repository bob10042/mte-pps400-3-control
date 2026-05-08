using System.Collections.ObjectModel;
using MtePpsControl.Protocol;

namespace MtePpsControl.ViewModels;

/// <summary>One row of an "extra-input" form (e.g. harmonic index, amplitude, phase).</summary>
public sealed class ExtraInputRow : ObservableObject
{
    public ExtraInputRow(ExtraInput def)
    {
        Definition = def;
        Value = def.Default;
    }
    public ExtraInput Definition { get; }
    public string Label => Definition.Label;
    public string Unit  => Definition.Unit;
    public bool IsInteger => Definition.IsInteger;
    /// <summary>Format hint shown next to the input — e.g. "integer 0..15" or "decimal 0..600".</summary>
    public string TypeHint
    {
        get
        {
            var t = Definition.IsInteger ? "integer" : "decimal";
            string range = (Definition.Min, Definition.Max) switch
            {
                (double mn, double mx) => $" {mn:G}..{mx:G}",
                (double mn, null)      => $" ≥{mn:G}",
                (null, double mx)      => $" ≤{mx:G}",
                _ => ""
            };
            return $"{t}{range}";
        }
    }

    private double _value;
    public double Value
    {
        get => _value;
        set
        {
            // Clamp to the spec range so we don't ever send out-of-range numbers.
            if (Definition.Min is double mn && value < mn) value = mn;
            if (Definition.Max is double mx && value > mx) value = mx;
            // Snap to integer if this input is integer-typed
            if (Definition.IsInteger) value = Math.Round(value);
            Set(ref _value, value);
        }
    }
}

/// <summary>One entry in the "last commands" log shown under the setter.</summary>
public sealed class CommandLogEntry
{
    public DateTime At { get; init; } = DateTime.Now;
    public string Sent { get; init; } = "";
    public string Received { get; init; } = "";
    public string Status { get; init; } = ""; // OK / E / ?
    public string DisplayLine =>
        $"{At:HH:mm:ss}  ▶ {Sent,-22}  ◀ {Received}";
}

/// <summary>
/// Drives the unified Parameter Setter UI. The user picks Category → Parameter → (Phase) → Values,
/// hits Apply, and we send the formatted RS-232 line.
/// </summary>
public sealed class ParameterSetterViewModel : ObservableObject
{
    private readonly Func<PpsClient?> _getClient;
    private readonly Func<bool> _isConnected;
    private readonly Action<string,string,string>? _opLogger;

    public ParameterSetterViewModel(Func<PpsClient?> getClient, Func<bool> isConnected,
                                    Action<string,string,string>? opLogger = null)
    {
        _getClient = getClient;
        _isConnected = isConnected;
        _opLogger = opLogger;

        Categories = new ObservableCollection<string>(ParameterCatalog.Categories);
        Parameters = new ObservableCollection<ParameterDefinition>();
        ExtraInputs = new ObservableCollection<ExtraInputRow>();
        Log = new ObservableCollection<CommandLogEntry>();

        // Build commands BEFORE first setter call (setters touch ApplyCommand.RaiseCanExecuteChanged).
        ApplyCommand     = new AsyncRelayCommand(ApplyAsync,    () => _isConnected() && SelectedParameter != null);
        ClearLogCommand  = new RelayCommand(() => Log.Clear());

        // Land on Load point + Voltage U as a sensible default
        SelectedCategory = ParameterCatalog.CatLoadPoint;
    }

    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<ParameterDefinition> Parameters { get; }
    public ObservableCollection<ExtraInputRow> ExtraInputs { get; }
    public ObservableCollection<CommandLogEntry> Log { get; }

    private string? _selectedCategory;
    public string? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!Set(ref _selectedCategory, value)) return;
            Parameters.Clear();
            if (value != null)
                foreach (var p in ParameterCatalog.ByCategory(value)) Parameters.Add(p);
            SelectedParameter = Parameters.FirstOrDefault();
        }
    }

    private ParameterDefinition? _selectedParameter;
    public ParameterDefinition? SelectedParameter
    {
        get => _selectedParameter;
        set
        {
            if (!Set(ref _selectedParameter, value)) return;
            // Detach old PropertyChanged handlers
            foreach (var old in ExtraInputs) old.PropertyChanged -= OnExtraInputChanged;
            // Rebuild the input form
            ExtraInputs.Clear();
            if (value != null)
            {
                if (value.ExtraInputs.Count == 0 && NeedsPrimaryValue(value))
                {
                    // Synthesize a single primary input row
                    var inp = new ExtraInput(
                        Label: $"{value.Name}",
                        Unit: value.Unit,
                        Min: value.MinValue, Max: value.MaxValue,
                        Default: 0);
                    ExtraInputs.Add(new ExtraInputRow(inp));
                }
                else
                {
                    foreach (var inp in value.ExtraInputs)
                        ExtraInputs.Add(new ExtraInputRow(inp));
                }
            }
            // Listen for value changes so the preview updates live
            foreach (var row in ExtraInputs) row.PropertyChanged += OnExtraInputChanged;
            Raise(nameof(NeedsPhase));
            Raise(nameof(ParameterNotes));
            Raise(nameof(PreviewLine));
            ApplyCommand.RaiseCanExecuteChanged();
        }
    }

    private void OnExtraInputChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExtraInputRow.Value)) Raise(nameof(PreviewLine));
    }

    private static bool NeedsPrimaryValue(ParameterDefinition p)
    {
        // Actions and read-only readbacks have no primary value — caller invokes Format with empty list.
        return p.Category != ParameterCatalog.CatAction && p.Category != ParameterCatalog.CatReadback;
    }

    public bool NeedsPhase => SelectedParameter?.PerPhase == true;
    public string? ParameterNotes => SelectedParameter?.Notes;

    /// <summary>Live preview of the exact RS-232 line(s) Apply would send right now.</summary>
    public string PreviewLine
    {
        get
        {
            var def = SelectedParameter;
            if (def is null) return "—";
            var values = ExtraInputs.Select(e => e.Value).ToList();
            if (!def.PerPhase) return def.Format(null, values);

            var picks = new List<int>();
            if (Phase1) picks.Add(1);
            if (Phase2) picks.Add(2);
            if (Phase3) picks.Add(3);
            if (picks.Count == 0) return "(no phase selected)";
            return string.Join("\n", picks.Select(p => def.Format(p, values)));
        }
    }

    // Independent per-phase checkboxes — Apply iterates whichever are checked.
    private bool _phase1 = true;
    public bool Phase1 { get => _phase1; set { if (Set(ref _phase1, value)) { Raise(nameof(PhaseSummary)); Raise(nameof(PreviewLine)); } } }
    private bool _phase2;
    public bool Phase2 { get => _phase2; set { if (Set(ref _phase2, value)) { Raise(nameof(PhaseSummary)); Raise(nameof(PreviewLine)); } } }
    private bool _phase3;
    public bool Phase3 { get => _phase3; set { if (Set(ref _phase3, value)) { Raise(nameof(PhaseSummary)); Raise(nameof(PreviewLine)); } } }

    public string PhaseSummary
    {
        get
        {
            var parts = new List<string>();
            if (Phase1) parts.Add("L1");
            if (Phase2) parts.Add("L2");
            if (Phase3) parts.Add("L3");
            return parts.Count == 0 ? "No phase selected — pick at least one"
                                    : "Will apply to: " + string.Join(" + ", parts);
        }
    }

    public RelayCommand SelectAllPhasesCommand => new(() => { Phase1 = Phase2 = Phase3 = true; });
    public RelayCommand SelectNoPhasesCommand  => new(() => { Phase1 = Phase2 = Phase3 = false; });

    public AsyncRelayCommand ApplyCommand { get; }
    public RelayCommand ClearLogCommand { get; }

    public void RaiseCanExecute() => ApplyCommand.RaiseCanExecuteChanged();

    private async Task ApplyAsync()
    {
        var client = _getClient();
        var def    = SelectedParameter;
        if (client is null || def is null) return;

        var values = ExtraInputs.Select(e => e.Value).ToList();
        // Determine which phase(s) to apply to
        IList<int?> phasesToApply;
        if (!def.PerPhase) phasesToApply = new int?[] { null };
        else
        {
            var picks = new List<int?>();
            if (Phase1) picks.Add(1);
            if (Phase2) picks.Add(2);
            if (Phase3) picks.Add(3);
            if (picks.Count == 0)
            {
                Log.Insert(0, new CommandLogEntry { Sent = def.Format(1, values),
                                                    Received = "<no phase selected — pick L1/L2/L3>",
                                                    Status = "ERR" });
                return;
            }
            phasesToApply = picks;
        }

        foreach (var phase in phasesToApply)
        {
            var line = def.Format(phase, values);
            try
            {
                var replies = await client.SendAsync(line);
                var received = string.Join(" | ", replies.Select(r => r.Raw));
                var status = replies.Any(r => r.IsOk)      ? "OK"
                           : replies.Any(r => r.IsError)   ? "E"
                           : replies.Any(r => r.IsUnknown) ? "?"
                           : replies.Count == 0            ? "—"
                                                           : "ok";
                Log.Insert(0, new CommandLogEntry { Sent = line, Received = received, Status = status });
                while (Log.Count > 100) Log.RemoveAt(Log.Count - 1);
                _opLogger?.Invoke(line, received, status);
            }
            catch (Exception ex)
            {
                Log.Insert(0, new CommandLogEntry { Sent = line, Received = $"<exception> {ex.Message}", Status = "ERR" });
                _opLogger?.Invoke(line, ex.Message, "ERR");
            }
        }
    }
}
