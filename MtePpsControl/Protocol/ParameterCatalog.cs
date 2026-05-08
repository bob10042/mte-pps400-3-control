using System.Globalization;

namespace MtePpsControl.Protocol;

/// <summary>One settable parameter on the PPS 400.3 — knows how to format its RS-232 command.</summary>
public sealed class ParameterDefinition
{
    public required string Category { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool PerPhase { get; init; }
    public string Unit { get; init; } = string.Empty;
    public double? MinValue { get; init; }
    public double? MaxValue { get; init; }
    /// <summary>Extra inputs beyond a single value (e.g. harmonics need index + amp + phi).</summary>
    public IReadOnlyList<ExtraInput> ExtraInputs { get; init; } = Array.Empty<ExtraInput>();
    /// <summary>Free-form note shown in the UI (limits, side-effects, etc.)</summary>
    public string? Notes { get; init; }
    /// <summary>Command formatter — takes phase number (1..3 or null) and value collection (primary first), returns the RS-232 line.</summary>
    public required Func<int?, IReadOnlyList<double>, string> Format { get; init; }

    public override string ToString() => Name;
}

public sealed record ExtraInput(string Label, string Unit = "", double? Min = null, double? Max = null, double Default = 0, bool IsInteger = false);

/// <summary>Static catalogue covering every settable parameter on the PPS 400.3.</summary>
public static class ParameterCatalog
{
    public const string CatLoadPoint = "Load point";
    public const string CatHarmonics = "Harmonics";
    public const string CatRipple    = "Ripple control";
    public const string CatSystem    = "System";
    public const string CatAction    = "Actions";
    public const string CatReadback  = "Readbacks / queries";

    private static string Inv(double d) => d.ToString(CultureInfo.InvariantCulture);

    public static IReadOnlyList<ParameterDefinition> All { get; } = new ParameterDefinition[]
    {
        // ---------- Load point ----------
        new() {
            Category = CatLoadPoint, Name = "Voltage U", Unit = "V", PerPhase = true,
            MinValue = 0, MaxValue = 600, Notes = "Phase-to-neutral, limited by UmaxLN (default 300 V).",
            Format = (p, v) => $"U{p},{Inv(v[0])}",
        },
        new() {
            Category = CatLoadPoint, Name = "Current I", Unit = "A", PerPhase = true,
            MinValue = 0, MaxValue = 120, Notes = "Limited by Imax.",
            Format = (p, v) => $"I{p},{Inv(v[0])}",
        },
        new() {
            Category = CatLoadPoint, Name = "Phase angle PH (U)", Unit = "°", PerPhase = true,
            MinValue = 0, MaxValue = 360, Notes = "Voltage phase angle.",
            Format = (p, v) => $"PH{p},{Inv(v[0])}",
        },
        new() {
            Category = CatLoadPoint, Name = "Angle φ (U-I)", Unit = "°", PerPhase = true,
            MinValue = 0, MaxValue = 360, Notes = "Phase shift between U and I on the same phase.",
            Format = (p, v) => $"W{p},{Inv(v[0])}",
        },
        new() {
            Category = CatLoadPoint, Name = "Frequency", Unit = "Hz", PerPhase = false,
            MinValue = 45, MaxValue = 400, Notes = "Fundamental frequency.",
            Format = (_, v) => $"FRQ{Inv(v[0])}",
        },
        new() {
            Category = CatLoadPoint, Name = "Voltage range BU", Unit = "", PerPhase = true,
            MinValue = 0, MaxValue = 99, Notes = "0 = auto / smallest possible.",
            Format = (p, v) => $"BU{p},{(int)v[0]}",
        },
        new() {
            Category = CatLoadPoint, Name = "Current range BI", Unit = "", PerPhase = true,
            MinValue = 0, MaxValue = 99, Notes = "0 = auto / smallest possible.",
            Format = (p, v) => $"BI{p},{(int)v[0]}",
        },
        new() {
            Category = CatLoadPoint, Name = "Voltage ramp RAMPU", Unit = "s", PerPhase = true,
            MinValue = 0, MaxValue = 600, Notes = "Ramp duration applied on the next SET.",
            Format = (p, v) => $"RAMPU{p},{Inv(v[0])}",
        },
        new() {
            Category = CatLoadPoint, Name = "Current ramp RAMPI", Unit = "s", PerPhase = true,
            MinValue = 0, MaxValue = 600, Notes = "Ramp duration applied on the next SET.",
            Format = (p, v) => $"RAMPI{p},{Inv(v[0])}",
        },
        new() {
            Category = CatLoadPoint, Name = "Big-I connector relay (SKLI)", Unit = "0|1", PerPhase = true,
            MinValue = 0, MaxValue = 1, Notes = "0 = open (low-I path), 1 = closed (high-I path). PPS-120A only.",
            Format = (p, v) => $"SKLI{p},{(int)v[0]}",
        },
        new() {
            Category = CatLoadPoint, Name = "Amplitude % of range (AU)", Unit = "%", PerPhase = true,
            Notes = "Voltage amplitude as % of internal end-of-range. Limits per PAR6..PAR7.",
            Format = (p, v) => $"AU{p},{Inv(v[0])}",
        },
        new() {
            Category = CatLoadPoint, Name = "Amplitude % of range (AI)", Unit = "%", PerPhase = true,
            Notes = "Current amplitude as % of internal end-of-range.",
            Format = (p, v) => $"AI{p},{Inv(v[0])}",
        },

        // ---------- Harmonics ----------
        new() {
            Category = CatHarmonics, Name = "Voltage harmonic (OWU)", Unit = "%", PerPhase = true,
            MinValue = 0, MaxValue = 40, Notes = "Index 2..6 max 40%, 7..31 max 10%. Sum ≤ 40%.",
            ExtraInputs = new[]
            {
                new ExtraInput("Harmonic index", Min: 2, Max: 31, Default: 3, IsInteger: true),
                new ExtraInput("Amplitude", "%", Min: 0, Max: 40, Default: 10),
                new ExtraInput("Phase angle", "°", Min: -360, Max: 360, Default: 0),
            },
            Format = (p, v) => $"OWU{p},{(int)v[0]},{Inv(v[1])},{Inv(v[2])}",
        },
        new() {
            Category = CatHarmonics, Name = "Current harmonic (OWI)", Unit = "%", PerPhase = true,
            ExtraInputs = new[]
            {
                new ExtraInput("Harmonic index", Min: 2, Max: 31, Default: 3, IsInteger: true),
                new ExtraInput("Amplitude", "%", Min: 0, Max: 40, Default: 10),
                new ExtraInput("Phase angle", "°", Min: -360, Max: 360, Default: 0),
            },
            Notes = "Index 2..6 max 40%, 7..31 max 10%.",
            Format = (p, v) => $"OWI{p},{(int)v[0]},{Inv(v[1])},{Inv(v[2])}",
        },
        new() {
            Category = CatHarmonics, Name = "Clear voltage harmonics on phase", PerPhase = true,
            Notes = "Erases all OWU harmonics (2..31) on the selected phase. No value needed.",
            Format = (p, _) => $"OWU{p},0,0",
        },
        new() {
            Category = CatHarmonics, Name = "Clear current harmonics on phase", PerPhase = true,
            Notes = "Erases all OWI harmonics (2..31) on the selected phase.",
            Format = (p, _) => $"OWI{p},0,0",
        },

        // ---------- Ripple control ----------
        new() {
            Category = CatRipple, Name = "Voltage ripple step (RCSU, absolute)", PerPhase = true,
            ExtraInputs = new[]
            {
                new ExtraInput("Duration", "ms", Min: 0, Default: 200),
                new ExtraInput("Amplitude", "V", Min: 0, Default: 10),
                new ExtraInput("Frequency", "Hz", Min: 0, Default: 166.7),
            },
            Notes = "Append one pulse to the voltage ripple telegram. Amp 0 = pause. Up to 250 steps.",
            Format = (p, v) => $"RCSU{p},{Inv(v[0])},{Inv(v[1])},{Inv(v[2])}",
        },
        new() {
            Category = CatRipple, Name = "Current ripple step (RCSI, absolute)", PerPhase = true,
            ExtraInputs = new[]
            {
                new ExtraInput("Duration", "ms", Min: 0, Default: 200),
                new ExtraInput("Amplitude", "A", Min: 0, Default: 1),
                new ExtraInput("Frequency", "Hz", Min: 0, Default: 166.7),
            },
            Notes = "Append one pulse to the current ripple telegram. Amp 0 = pause.",
            Format = (p, v) => $"RCSI{p},{Inv(v[0])},{Inv(v[1])},{Inv(v[2])}",
        },
        new() {
            Category = CatRipple, Name = "Voltage ripple step (% of fundamental)", PerPhase = true,
            ExtraInputs = new[]
            {
                new ExtraInput("Duration", "ms", Min: 0, Default: 200),
                new ExtraInput("Amplitude", "%", Min: 0, Max: 100, Default: 10),
                new ExtraInput("Frequency", "Hz", Min: 0, Default: 166.7),
            },
            Format = (p, v) => $"RCSUP{p},{Inv(v[0])},{Inv(v[1])},{Inv(v[2])}",
        },
        new() {
            Category = CatRipple, Name = "Current ripple step (% of fundamental)", PerPhase = true,
            ExtraInputs = new[]
            {
                new ExtraInput("Duration", "ms", Min: 0, Default: 200),
                new ExtraInput("Amplitude", "%", Min: 0, Max: 100, Default: 10),
                new ExtraInput("Frequency", "Hz", Min: 0, Default: 166.7),
            },
            Format = (p, v) => $"RCSIP{p},{Inv(v[0])},{Inv(v[1])},{Inv(v[2])}",
        },
        new() {
            Category = CatRipple, Name = "Stop voltage ripple on phase", PerPhase = true,
            Notes = "Aborts any running RCSU telegram on the selected phase and erases its table.",
            Format = (p, _) => $"RCSU{p},0",
        },
        new() {
            Category = CatRipple, Name = "Stop current ripple on phase", PerPhase = true,
            Notes = "Aborts any running RCSI telegram and erases its table.",
            Format = (p, _) => $"RCSI{p},0",
        },

        // ---------- System parameters ----------
        new() {
            Category = CatSystem, Name = "Set parameter PAR<n>", PerPhase = false,
            ExtraInputs = new[]
            {
                new ExtraInput("Parameter number", Min: 1, Max: 999, Default: 201, IsInteger: true),
                new ExtraInput("Value", Default: 0),
            },
            Notes = "1..99 = system params, 100..199 = all-DSP write, 200+ = per-DSP. Service-staff territory.",
            Format = (_, v) => $"PAR{(int)v[0]},{Inv(v[1])}",
        },
        new() {
            Category = CatSystem, Name = "Read parameter PAR<n>", PerPhase = false,
            ExtraInputs = new[] { new ExtraInput("Parameter number", Min: 1, Max: 999, Default: 201, IsInteger: true) },
            Notes = "Reply contains the current value.",
            Format = (_, v) => $"PAR{(int)v[0]}",
        },
        new() {
            Category = CatSystem, Name = "Save parameters to flash (PAF)", PerPhase = false,
            ExtraInputs = new[] { new ExtraInput("Mask", Min: 0, Max: 15, Default: 1, IsInteger: true) },
            Notes = "Mask = 1 (system) + 2 (DSP/range) + 4 (user mem) + 8 (load points). 0 = all.",
            Format = (_, v) => $"PAF{(int)v[0]}",
        },
        new() {
            Category = CatSystem, Name = "Measurement time base (T)", Unit = "s", PerPhase = false,
            MinValue = 0.05, MaxValue = 60, Notes = "How long ?<n> averages over. Default 2s.",
            Format = (_, v) => $"T{Inv(v[0])}",
        },
        new() {
            Category = CatSystem, Name = "Comms mode (MODE)", Unit = "0|1", PerPhase = false,
            MinValue = 0, MaxValue = 1, Notes = "0 = compatibility, 1 = extended framed replies.",
            Format = (_, v) => $"MODE{(int)v[0]}",
        },
        new() {
            Category = CatSystem, Name = "Auto-result streaming (SP)", PerPhase = false,
            ExtraInputs = new[]
            {
                new ExtraInput("Result number (0=all, 1=I, 2=U, 10=THDI, 11=THDU, 12=PhiI, 13=PhiU)", Min: 0, Max: 13, Default: 0, IsInteger: true),
                new ExtraInput("Flag (0=disable, 1=enable, 2=enable-only-this)", Min: 0, Max: 2, Default: 1, IsInteger: true),
            },
            Notes = "Steers which measurement results stream automatically. SP0,1 enables all; SP0,0 disables all.",
            Format = (_, v) => $"SP{(int)v[0]},{(int)v[1]}",
        },

        // ---------- Actions (no value needed) ----------
        new() {
            Category = CatAction, Name = "SET (apply queued setpoints)", Notes = "Commits all pending changes to the unit.",
            Format = (_, _) => "SET",
        },
        new() {
            Category = CatAction, Name = "OFF (ramped, all amps)", Notes = "Smooth shutdown using last-set ramps.",
            Format = (_, _) => "OFF0",
        },
        new() {
            Category = CatAction, Name = "EMERGENCY (immediate kill all)", Notes = "Hard kill, no ramp — fastest possible shutdown.",
            Format = (_, _) => "OFF1",
        },
        new() {
            Category = CatAction, Name = "OFF U-amps only (immediate)", Format = (_, _) => "OFF2" },
        new() {
            Category = CatAction, Name = "OFF I-amps only (immediate)", Format = (_, _) => "OFF3" },
        new() {
            Category = CatAction, Name = "ON (re-engage U+I after OFF)",   Format = (_, _) => "ON1" },
        new() {
            Category = CatAction, Name = "ON U only", Format = (_, _) => "ON2" },
        new() {
            Category = CatAction, Name = "ON I only", Format = (_, _) => "ON3" },
        new() {
            Category = CatAction, Name = "Reset DSP (R) — reboots generator!",
            Notes = "Reboots the source software. All unsaved settings are lost.",
            Format = (_, _) => "R",
        },

        // ---------- Readbacks (one-shot queries; the poll loop handles the regular ones) ----------
        new() { Category = CatReadback, Name = "Version (VER)",                Notes = "Identity / firmware / module / build date.",
                Format = (_, _) => "VER" },
        new() { Category = CatReadback, Name = "Busy flag (BSY)",              Notes = "0 = ready, 1 = SET still ramping.",
                Format = (_, _) => "BSY" },
        new() { Category = CatReadback, Name = "Status flags (SE)",            Notes = "Error/Warning + DOS-U/DOS-I/Busy/Packet/Ripple bits.",
                Format = (_, _) => "SE" },
        new() { Category = CatReadback, Name = "Generator status (SG)",        Notes = "Manual/parameterization mode + GEN ring status.",
                Format = (_, _) => "SG" },
        new() { Category = CatReadback, Name = "Voltage amp status (STATU)",   Notes = "Per-phase: 0 off, 1 on, 2 idle/overload, 3 err-off, 4 dis-off.",
                Format = (_, _) => "STATU" },
        new() { Category = CatReadback, Name = "Current amp status (STATI)",
                Format = (_, _) => "STATI" },
        new() { Category = CatReadback, Name = "Measured currents (?1)",       Notes = "3-phase RMS current readback.",
                Format = (_, _) => "?1" },
        new() { Category = CatReadback, Name = "Measured voltages (?2)",
                Format = (_, _) => "?2" },
        new() { Category = CatReadback, Name = "THD current (?10)",
                Format = (_, _) => "?10" },
        new() { Category = CatReadback, Name = "THD voltage (?11)",
                Format = (_, _) => "?11" },
        new() { Category = CatReadback, Name = "Phi current absolute (?12)",
                Format = (_, _) => "?12" },
        new() { Category = CatReadback, Name = "Phi voltage absolute (?13)",
                Format = (_, _) => "?13" },
        new() { Category = CatReadback, Name = "Voltage ripple status",        PerPhase = true,
                Notes = "0=idle, 0.0=ready, 0.1..100.0=running %.",
                Format = (p, _) => $"RCSU{p}" },
        new() { Category = CatReadback, Name = "Current ripple status",        PerPhase = true,
                Format = (p, _) => $"RCSI{p}" },
        new() { Category = CatReadback, Name = "Voltage spectrum dump (RSU)",  PerPhase = true,
                Notes = "Hex DSP spectrum — 32 bins × (real,imag) two's-complement.",
                Format = (p, _) => $"RSU{p}" },
        new() { Category = CatReadback, Name = "Current spectrum dump (RSI)",  PerPhase = true,
                Format = (p, _) => $"RSI{p}" },
    };

    public static IReadOnlyList<string> Categories { get; } =
        All.Select(p => p.Category).Distinct().ToList();

    public static IEnumerable<ParameterDefinition> ByCategory(string cat) =>
        All.Where(p => p.Category == cat);
}
