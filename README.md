# MTE PPS 400.3 Control

A Windows desktop GUI for controlling and logging an **MTE PPS 400.3 Portable Power Source** (built into a PCS 400.3 control module) over RS-232.

Built and tested live against `PPS400.3 #28492`, firmware `V3.07`, build `11.10.2005` on a Prolific PL2303 USB-to-serial adapter.

## What this is

The PPS 400.3 is a 3-phase programmable AC power source originally designed for energy-meter calibration labs. It exposes a 37-command ASCII protocol over RS-232 at 19200 8N1. The unit's own touchscreen (the PCS 400.3 controller) is aging and the backlight is dim, so this app gives you a clean modern Windows interface to:

- Drive every settable parameter the unit supports (per-phase voltage, current, phase angles, ramps, ranges, harmonics, ripple control, the high-current connector relay, system parameters)
- Read every measurement the unit returns (RMS V/I, THD, absolute phase angles, status flags)
- Compute derived values the unit doesn't report (active / reactive / apparent power and power factor — per phase and as 3-phase totals)
- Log every command, reply, action, and measurement to a SQLite database for full traceability
- Optionally log V/I tick data to CSV for Excel analysis
- Save and load complete test configurations (source state + harmonics + ripple telegrams) as JSON presets
- Run multi-step test sequences automatically

The GUI is portable — it auto-detects the COM port that responds to a `VER` probe with a "PPS" reply, so it works on any laptop without hard-coded port settings.

## Hardware identity

```
VER →  PPS400.3 #28492     (model + serial)
       3.07                 (firmware)
       OPRW                 (module type)
       11.10.2005           (build date)
```

- 3-phase programmable AC source, 120 A variant
- 3 RS-232 ports on the unit; only the rear `RS232` port currently responds at 19200 baud (the front two are PCS-side, different protocol)
- Connection: 19200 8N1, no handshake, CR-terminated

## Features

### Unified Parameter Setter (the main page)
- Three-pane cascade: **Category** → **Parameter** → **Value form**
- Categories: Load point / Harmonics / Ripple control / System / Actions / Readbacks
- Per-phase parameters apply to any combination of L1 / L2 / L3 (multi-select with All / None shortcuts)
- Big "SETTING: <param>" header at the top of the value form, always tells you what's being set
- Recent-commands log on the right shows exact RS-232 line + reply + status pill (OK/E/?/ERR)
- CSV record start/stop button

### Source Control (legacy 3-phase matrix view)
- Per-phase setpoint grid for U / I / PH / φ
- Frequency, U-ramp, I-ramp
- Auto-range or manual range select per amplifier
- Big-I connector relay (SKLI) toggle per phase
- OUTPUT-ON banner (green when off, red when any amp energized)
- SET / OFF (ramped) / EMERGENCY (immediate kill) action buttons, always docked at the bottom

### Harmonics editor
- OWI / OWU injection 2..15 with per-row enable, amplitude (%) and phase offset
- Phase + V/I selector
- Push & Clear

### Ripple-control telegram editor
- RCSI / RCSU / RCSIP / RCSUP step lists
- Phase + V/I + absolute/% selector
- Up to 250 steps per phase

### Sequence runner
- Capture-from-source button (snapshot the current setpoints as a step)
- Reorder (↑ / ↓), edit hold time per step
- Run / Stop with auto-OFF at the end

### Database
- JSON presets at `%APPDATA%\MtePpsControl\presets.json`
- Each preset captures the full state: source + harmonics + ripple

### Persistent operational log
- SQLite database at `%APPDATA%\MtePpsControl\operational_log.sqlite`
- Three tables: `commands`, `measurements`, `sessions`
- Captures every Apply, every poll tick, every connect/disconnect — independently of CSV recording

### Auto-detect / safety
- COM-port probe at startup (any baud-correct port that returns "PPS" in `VER`)
- Last-good port cached at `%APPDATA%\MtePpsControl\settings.json`
- Auto-OFF on disconnect if any amp is energized
- Re-entry guards on Connect / AutoDetect prevent the second-attempt path
- Catch-suppress dialog when connection is already established
- Collapsible side panel (◀ / ▶) so views aren't covered on small screens

## Architecture

```
MtePpsControl/
├── Protocol/                       The RS-232 layer
│   ├── PpsClient.cs               async client, all 37 commands
│   ├── PpsAutoDetect.cs           VER probe across ports
│   ├── PpsReply.cs                MODE 0/1 reply parser
│   ├── PhaseStatus.cs             STATU/STATI enum decode
│   ├── StatusFlags.cs             SE flag bit decode
│   ├── PpsCalculations.cs         P / Q / S / PF math
│   └── ParameterCatalog.cs        full settable surface
├── ViewModels/                     INPC / RelayCommand-based MVVM
│   ├── MainViewModel.cs           connection, polling, navigation
│   ├── ParameterSetterViewModel.cs cascade + Apply
│   ├── HarmonicsViewModel.cs      OWI/OWU
│   ├── RippleViewModel.cs         RCSI/RCSU
│   ├── SequenceViewModel.cs       step runner
│   ├── DatabaseViewModel.cs       presets persistence
│   ├── PhaseSetpointViewModel.cs  per-phase row + computed P/Q/S/PF
│   ├── CsvLogger.cs               Excel-friendly tick log
│   ├── OperationalLog.cs          SQLite persistent log
│   └── AppSettings.cs             last-good port etc.
├── Views/                          XAML + minimal code-behind
│   ├── ParameterSetterView.xaml   the unified setter
│   ├── SourceControlView.xaml     3-phase matrix
│   ├── HarmonicsView.xaml
│   ├── RippleView.xaml
│   ├── SequenceView.xaml
│   ├── DatabaseView.xaml
│   └── ViewBase.cs                sub-view DataContext attach helper
└── Resources/
    ├── Theme.xaml                 light silver / MTE-blue theme
    └── Converters.cs              BoolToVis, StrToVis, etc.
```

### Design decisions worth knowing

- **Light silver/blue theme** matches the PCS 400.3 touchscreen aesthetic. Dark themes were tried and rejected as too detached from the unit.
- **DataContext for sub-views attached on `Loaded`** via `ViewDataContextHelper.AttachWhenLoaded(...)`. The simpler pattern of `DataContext="{Binding Setter, RelativeSource=AncestorType=Window}"` silently fails because Style.Setter values are parsed before the visual-tree ancestor is in scope. Items appeared empty until this was fixed.
- **Open-retry with backoff** on `PpsClient.Open()` — Prolific USB-serial drivers don't release handles instantly after Close; back-to-back probe-then-connect would race without the retry.
- **Reply-frame-aware parser** — the PPS firmware emits `?<n>` measurement replies with a leading comma after the code byte (`E@,v1,v2,v3` not `E@v1,v2,v3` as the spec implies). The parser strips leading separators and takes the last 3 numeric fields.
- **Computed PQS** is client-side. The PPS reports U, I, THDU, THDI, PhiU, PhiI but not active/reactive/apparent power — `PpsCalculations` derives them from the reported quantities.

## Build & run

Requires .NET 8 SDK on Windows.

```powershell
git clone https://github.com/bob10042/mte-pps400-3-control.git
cd mte-pps400-3-control\MtePpsControl
dotnet build
.\bin\Debug\net8.0-windows\MtePpsControl.exe
```

Connect a USB-to-serial cable to the rear `RS232` port on the unit, power on, and the app should auto-detect within a few seconds. `MODE` is set to `1` (extended replies) automatically on connect.

## Verified behaviour (live tests, May 2026)

| Test | Result |
|---|---|
| `I1 = 0.1 A` round trip via current loopback | measured 0.0949 A |
| `U1 = 10 V` round trip vs external multimeter | measured 10.000 V |
| Voltage sweep 50/100/150/200/250/300 V | within calibration |
| 200 V + 2 A high-current path (SKLI=1) | 200.001 V / 2.00021 A |
| 200 V + 1 A standard path (SKLI=0) | 200.000 V / 0.999995 A |
| Balanced 3-phase U=10V I=1A φ=30° | P = 86.55 W (ideal 86.60), PF = 0.8660 (exact) |
| 10% 3rd voltage harmonic on L1 via OWU | THDU = 10.00% measured |
| Silent injection of all 28 parameter categories | 64/64 commands accepted |

The unit normalises outputs in unbalanced single-phase configurations (drives one phase alone at half voltage). This is internal balance compensation, not a software bug — drive balanced 3-phase for accurate readings.

## Known limitations

- The four greyed icon-strip buttons (kWh meter, A/V meter, Wattmeter test, CT test) need PCS-side comms which haven't been reverse-engineered yet
- The unit's internal voltage sensor reads ~1.5% low at the top of the 300 V range. External meter is the ground truth
- MTE service parameters (PAR<n> for n>=100) shouldn't be set casually — the GUI exposes them but with a warning note
- The unit's touchscreen backlight is significantly dimmed by age; nothing this app can do about it

## Logs and persistence

| Path | Format | Contents |
|---|---|---|
| `%APPDATA%\MtePpsControl\settings.json` | JSON | Last-good COM port, auto-connect-on-start flag |
| `%APPDATA%\MtePpsControl\presets.json` | JSON | Saved test-configuration presets |
| `%APPDATA%\MtePpsControl\operational_log.sqlite` | SQLite | All commands, replies, measurements, sessions |
| `%APPDATA%\MtePpsControl\logs\vi-log_<ts>.csv` | CSV | One Excel-friendly file per recording session |

## Probe scripts (Python, kept for diagnostics)

- `probe_pps.py` — minimal `VER` probe that confirmed initial comms
- `probe_pps2.py` — broader baud / handshake sweep used for diagnosis
- `probe_pcs.py` — sweep variant for the unresponsive front-panel ports
- `probe_state.py` — full read-only state dump
- `silent_full_test.py` — multi-channel verification including computed PQS
- `inject_all_params.py` — silent injection of every parameter category (64/64 ✓)
- `test_harmonics_and_log.py` — harmonics injection with CSV logging
- `test_channel_3.py` — per-channel isolation diagnostic

## License & contact

This project is an internal tool for instrument control on bench. The MTE PPS 400.3 documentation is © MTE Meter Test Equipment AG / EMH Energie-Messtechnik GmbH and is not redistributed here.

Originating session: `claude/jovial-heisenberg-8b4eb4` worktree, May 2026.
