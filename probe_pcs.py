"""Probe PCS 400.3 front-panel RS232 (A or B) — sweep bauds 4800..115200."""
import serial, time

PORT = 'COM5'
bauds = [4800, 9600, 19200, 38400, 57600, 115200]

for baud in bauds:
    try:
        s = serial.Serial(PORT, baudrate=baud, bytesize=8, parity='N', stopbits=1,
                          timeout=1.5, rtscts=False, dsrdtr=False, xonxoff=False,
                          write_timeout=2)
    except Exception as e:
        print(f'{baud}: open err {e}')
        continue
    s.dtr = True; s.rts = True
    s.reset_input_buffer(); s.reset_output_buffer()
    time.sleep(0.1)
    s.write(b'VER\r')
    time.sleep(0.7)
    resp = s.read_all()
    print(f'baud={baud:>6}  rx ({len(resp)} bytes): {resp[:200]!r}')
    s.close()
