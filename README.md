# MTE PPS 400.3 control

Working notes and probe scripts for talking to an **MTE PPS 400.3-120A** Portable Power Source over RS-232.

## Unit identity (verified 2026-05-07)

```
VER →  PPS400.3 #28492    (model + serial)
       3.07                (firmware)
       OPRW                (module type)
       11.10.2005          (build date)
```

- 3-phase programmable AC/DC power source
- 120 A version (high-current 6 mm connectors + standard 4 mm sockets, internally tied)
- 3 RS-232 ports on the top panel; only one currently responds at the documented 19200 baud

## PC connection

| Item | Value |
|---|---|
| Adapter | Prolific PL2303GT USB-to-RS232 |
| COM port | COM5 |
| Baud | 19200 |
| Format | 8 data, no parity, 1 stop, no handshake |
| Cable | Standard 9-pin (the one that works went straight to the rear-most labelled "RS232" port) |
| Terminator | `\r` (CR, 0x0D) — also accepts `;` |

## Protocol

ASCII line-based, **not SCPI**. Plain command codes with optional parameters separated by commas.

```
<COMMAND>[<param>[,<param>...]]<CR>
```

Errors echo `<command>=E<CR>`. Max line length 190 chars.

37-command set covers: `MODE`, `?<n>`, `AI/AU`, `BI/BU`, `BSY`, `FRQ`, `I/U`, `OFF/ON`, `OWI/OWU`, `PAF/PAR`, `PH/W`, `R`, `RAMPI/RAMPU`, `RCSI/RCSU/RCSIP/RCSUP`, `RSI/RSU`, `SE`, `SET`, `SG`, `SKLI`, `SP`, `STATI/STATU`, `T`, `VER`. Full descriptions in `MTE/PTS 400.3/PPS400.3 RS232 Interface description english.pdf` (kept locally, not in repo).

## Files

- `probe_pps.py` — minimal `VER` probe that confirmed comms on 2026-05-07
- `probe_pps2.py` — broader baud / handshake sweep (used for diagnosis)
- `probe_pcs.py` — sweep variant for the unresponsive front-panel ports

## Next session (planned)

Build a Python+Tkinter GUI replicating the unit's full front-panel control:
- Phase 1: V/I/F per phase, range, ON/OFF, mode, EMERGENCY OFF, live status readback, error log
- Phase 2: harmonics editor, ripple control, ramps, DC mode
- Phase 3: presets, CSV logging, scripted sequences, joint verification with the Keysight 34470A

Start the next session by pulling `SET` and `SG` to see current state.
