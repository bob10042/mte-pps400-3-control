"""Deeper PPS probe: try more bauds, force RTS/DTR, try various terminators."""
import serial, time

PORT = 'COM5'
bauds = [4800, 9600, 19200, 38400, 57600, 115200]
terminators = [b'\r', b';', b'\r\n', b'\n']

for baud in bauds:
    for hs in [False, True]:
        try:
            s = serial.Serial(PORT, baudrate=baud, bytesize=8, parity='N', stopbits=1,
                              timeout=1.5, rtscts=hs, dsrdtr=False, xonxoff=False)
        except Exception as e:
            print(f'  open err {baud} hs={hs}: {e}'); continue
        # Force handshake lines high
        s.dtr = True; s.rts = True
        s.reset_input_buffer(); s.reset_output_buffer()
        time.sleep(0.1)
        any_rx = b''
        for term in terminators:
            cmd = b'VER' + term
            s.write(cmd)
            time.sleep(0.5)
            resp = s.read_all()
            if resp:
                any_rx += b'[' + term + b']' + resp
        print(f"  baud={baud:>6} rtscts={hs!s:5}  rx: {any_rx[:80]!r}")
        s.close()
