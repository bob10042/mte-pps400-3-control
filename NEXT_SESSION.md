# Next session — pickup notes

Last session: **2026-05-08**, branch `claude/jovial-heisenberg-8b4eb4` (also `main` on GitHub).

## State of the world

- **Code:** WPF C# app at `MtePpsControl/`. Builds clean with `dotnet build`. Run from `MtePpsControl/bin/Debug/net8.0-windows/MtePpsControl.exe`.
- **Tested live** against `PPS400.3 #28492` (fw 3.07, build 11.10.2005) over COM5 @ 19200 8N1.
- **Last commit:** `22a20d6 — Setter format safety: live preview, range hints, invariant-culture parsing`
- **Top of `main` and feature branch are at the same commit.** No outstanding diffs.

## Confirmed working

| Subsystem | Status |
|---|---|
| Auto-detect + connect | ✅ |
| All 37 RS-232 commands → 64/64 silent injection passed | ✅ |
| Setter cascade: Category → Parameter → Phase checkboxes → Values → Apply | ✅ |
| **Live `WILL SEND` preview** of the exact RS-232 line(s) before Apply | ✅ |
| Per-input type hints (decimal vs integer, range) | ✅ |
| InvariantCulture parsing (no comma/period locale traps) | ✅ |
| Auto-clamp to spec range + integer rounding | ✅ |
| Phase multi-select (L1/L2/L3 checkboxes + All/None) | ✅ |
| Computed `P / Q / S / PF` per phase + 3-phase totals | ✅ |
| CSV V/I logger (Excel-friendly, toggleable) | ✅ |
| SQLite operational log (every command + every poll tick) | ✅ |
| Connection chip with green dot + auto-OFF on disconnect | ✅ |
| Collapsible side panel | ✅ |
| Bright blue header with connected unit's identity line | ✅ |
| Light silver/blue theme matching the touchscreen | ✅ |

## Still pending

1. **DB-driven runner** — saved presets / sequences / harmonic sets / ripple telegrams in the Database view should each have a Run button that plays the stored item back automatically. Schema is in `OperationalLog` and `Preset.cs`; runner needs wiring in `DatabaseViewModel`.
2. **Manual click-through audit of the four legacy sub-views** — Harmonics, Ripple, Sequence, Database. The same DataContext fix that made the Setter work was applied via `ViewBase.cs` → `AttachWhenLoaded`, but UI Automation tree-scope inspection isn't reliable for visibility (it returns hidden elements too). Click each side-panel icon and confirm items appear.
3. **PCS-side protocol exploration** — the four greyed icon-strip entries (kWh meter, A/V meter, Wattmeter test, CT test) need PCS-side comms which haven't been reverse-engineered. The unit's PRS400.3 RS232 manual (also on disk) is the next document to read.
4. **Repo metadata on GitHub** — add description + topics. Either via web UI (About → gear icon) or `gh auth login` then ask me.

## Where things live

- **Code:** `MtePpsControl/` — `Protocol/`, `ViewModels/`, `Views/`, `Resources/`
- **Probe scripts** (Python, kept for diagnostics): repo root + parent `MTE/` directory has more
- **Logs at runtime:** `%APPDATA%\MtePpsControl\` — `settings.json`, `presets.json`, `operational_log.sqlite`, `logs\vi-log_*.csv`
- **Spec PDFs** (gitignored, kept locally): `C:\Users\Robert\Downloads\MTE\PTS 400.3\` — `PPS400.3 RS232 Interface description english.pdf` is the authoritative protocol reference

## Quick start for a fresh session

```powershell
# Clone if not already
git clone https://github.com/bob10042/mte-pps400-3-control.git
cd mte-pps400-3-control

# Build & run
cd MtePpsControl
dotnet build
.\bin\Debug\net8.0-windows\MtePpsControl.exe

# Plug USB-to-serial into the rear RS232 port on the unit, power it on
# App auto-detects within ~3 seconds. MODE=1 set automatically.
```

If the GUI doesn't auto-connect, kill any orphan `dotnet`/`MtePpsControl` processes first — they may still be holding the COM port from a previous run.

## Memory entries (in `~/.claude/projects/...MTE/memory/`)

- `project_mte_device_identity.md` — hardware identity (PCS 400.3 + PPS 400.3 #28492)
- `project_mte_gui_state.md` — full snapshot of what was built, what works, what's pending
- `reference_pps_rs232_quick.md` — command quick-reference + firmware quirks
- `feedback_user_design_preferences.md` — the user's repeated design preferences

## Verified live (last session)

- Voltage round-trip: `U1 = 10 V` → measured 10.000 V (multimeter)
- Current loopback: `I1 = 0.1 A` → 0.0949 A
- Voltage sweep 50/100/150/200/250/300 V at 10 A and 2 A, both connector paths — all clean
- Balanced 3-phase 10 V / 1 A / φ=30°: P = 86.55 W (ideal 86.60), PF = 0.8660 exact
- Harmonic injection: 10% commanded → 10.00% measured THDU on the targeted phase, others unaffected
- Ramped OFF / EMERGENCY OFF / re-engage all verified
