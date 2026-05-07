"""Probe PPS 400.3 over COM5 with the documented direct-connect parameters."""
import sys
try:
    import serial
except ImportError:
    print("Installing pyserial...")
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "pyserial"])
    import serial

import time

PORT = 'COM5'
configs = [
    # (baud, parity, bytesize, stopbits, label)
    (19200, 'N', 8, 1, 'PPS direct'),
    (9600,  'N', 8, 1, 'fallback 9600'),
    (115200,'N', 8, 1, 'PCS high'),
]

for baud, par, bs, sb, label in configs:
    print(f"\n--- {label}: {baud} {bs}{par}{sb} ---")
    try:
        s = serial.Serial(PORT, baudrate=baud, parity=par, bytesize=bs, stopbits=sb,
                          timeout=2, rtscts=False, dsrdtr=False, xonxoff=False)
    except Exception as e:
        print(f"  open err: {e}")
        continue
    s.reset_input_buffer(); s.reset_output_buffer()
    for cmd in ['VER\r', 'VER;', '?\r']:
        s.write(cmd.encode())
        time.sleep(0.6)
        resp = s.read_all()
        print(f"  TX {cmd!r:>10s}  RX  ({len(resp)} bytes) {resp!r}")
    s.close()
