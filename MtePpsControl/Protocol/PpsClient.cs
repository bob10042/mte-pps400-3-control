using System.Globalization;
using System.IO.Ports;
using System.Text;

namespace MtePpsControl.Protocol;

/// <summary>
/// Async serial client for the MTE PPS 400.3 source module.
/// Direct connection at 19200 8N1, CR-terminated ASCII, MODE 0 or MODE 1 reply framing.
/// </summary>
public sealed class PpsClient : IAsyncDisposable
{
    private readonly SerialPort _port;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly TimeSpan _interReplyQuiet = TimeSpan.FromMilliseconds(120);
    private bool _modeOne;

    public PpsClient(string portName, int baud = 19200)
    {
        _port = new SerialPort(portName, baud, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = false,
            RtsEnable = false,
            ReadTimeout = 1500,
            WriteTimeout = 1500,
            NewLine = "\r",
            Encoding = Encoding.ASCII,
        };
    }

    public bool IsOpen => _port.IsOpen;
    public bool ModeExtended => _modeOne;

    public void Open()
    {
        if (_port.IsOpen) return;
        // USB-serial drivers (Prolific especially) don't immediately release the handle after
        // a Close(). If we just probed this port via PpsAutoDetect, the first Open() can throw
        // UnauthorizedAccess or IOException. Retry with a short backoff before giving up.
        Exception? last = null;
        var delays = new[] { 0, 250, 500, 800 };
        foreach (var d in delays)
        {
            if (d > 0) Thread.Sleep(d);
            try
            {
                _port.Open();
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();
                return;
            }
            catch (UnauthorizedAccessException ex) { last = ex; }
            catch (System.IO.IOException ex)        { last = ex; }
        }
        throw last ?? new InvalidOperationException("Failed to open serial port");
    }

    public void Close()
    {
        if (_port.IsOpen) _port.Close();
    }

    /// <summary>Switch to MODE 1 (extended replies). Idempotent and tolerant of MODE 0 framing.</summary>
    public async Task SetExtendedModeAsync(CancellationToken ct = default)
    {
        var lines = await SendAsync("MODE1", ct);
        // In MODE 0, the reply may be empty or just '1'. In MODE 1, "MODE1=O".
        _modeOne = lines.Any(l => l.Raw.Contains("MODE1=O"))
                || lines.Any(l => l.Raw == "1")
                || lines.Count == 0;
    }

    /// <summary>Send a command and read all CR-terminated reply lines until quiet.</summary>
    public async Task<IReadOnlyList<PpsReply>> SendAsync(string command, CancellationToken ct = default)
    {
        if (!_port.IsOpen) throw new InvalidOperationException("Port not open");
        await _mutex.WaitAsync(ct);
        try
        {
            _port.DiscardInBuffer();
            await _port.BaseStream.WriteAsync(Encoding.ASCII.GetBytes(command + "\r"), ct);

            var sb = new StringBuilder();
            var buf = new byte[256];
            var quietUntil = DateTime.UtcNow + TimeSpan.FromMilliseconds(1500);
            while (DateTime.UtcNow < quietUntil)
            {
                if (_port.BytesToRead > 0)
                {
                    var n = await _port.BaseStream.ReadAsync(buf, ct);
                    if (n > 0)
                    {
                        sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                        quietUntil = DateTime.UtcNow + _interReplyQuiet;
                    }
                }
                else
                {
                    await Task.Delay(15, ct);
                }
            }

            return sb.ToString()
                .Split('\r', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => new PpsReply(s.Trim()))
                .ToList();
        }
        finally { _mutex.Release(); }
    }

    // ---- Convenience accessors over the protocol ----

    public async Task<string> VersionAsync(CancellationToken ct = default)
    {
        var lines = await SendAsync("VER", ct);
        return string.Join(" | ", lines.Select(l => l.Raw));
    }

    public async Task<bool> IsBusyAsync(CancellationToken ct = default)
    {
        var lines = await SendAsync("BSY", ct);
        return lines.Any(l => l.Body.Trim() == "1");
    }

    public async Task<StatusFlags> StatusAsync(CancellationToken ct = default)
    {
        var lines = await SendAsync("SE", ct);
        foreach (var l in lines)
        {
            // Body of "SE=ExxSxxxxx" -> strip "SE=" if present
            var body = l.Body;
            if (StatusFlags.TryParse(body, out var f)) return f;
            // MODE 0: line is the body itself
            if (StatusFlags.TryParse(l.Raw, out f)) return f;
        }
        return StatusFlags.Empty;
    }

    public async Task<PhaseStatus[]> ReadVoltagePhaseStatusAsync(CancellationToken ct = default)
        => await ReadPhaseStatusAsync("STATU", ct);

    public async Task<PhaseStatus[]> ReadCurrentPhaseStatusAsync(CancellationToken ct = default)
        => await ReadPhaseStatusAsync("STATI", ct);

    private async Task<PhaseStatus[]> ReadPhaseStatusAsync(string cmd, CancellationToken ct)
    {
        var lines = await SendAsync(cmd, ct);
        var result = new PhaseStatus[3] { PhaseStatus.Unknown, PhaseStatus.Unknown, PhaseStatus.Unknown };
        foreach (var l in lines)
        {
            // Lines look like "STATU1=2" — head ends in 1/2/3
            var head = l.Head;
            if (head.Length == 0) continue;
            if (!int.TryParse(head[^1..], out var phase)) continue;
            if (phase < 1 || phase > 3) continue;
            if (l.TryInt(out var v)) result[phase - 1] = (PhaseStatus)v;
        }
        return result;
    }

    public async Task<double[]> ReadVoltageSetpointsAsync(CancellationToken ct = default)
        => await ReadTriPhaseAsync("U", ct);

    public async Task<double[]> ReadCurrentSetpointsAsync(CancellationToken ct = default)
        => await ReadTriPhaseAsync("I", ct);

    public async Task<double[]> ReadVoltagePhaseAnglesAsync(CancellationToken ct = default)
        => await ReadTriPhaseAsync("PH", ct);

    public async Task<double[]> ReadPhiAsync(CancellationToken ct = default)
        => await ReadTriPhaseAsync("W", ct);

    private async Task<double[]> ReadTriPhaseAsync(string cmd, CancellationToken ct)
    {
        var lines = await SendAsync(cmd, ct);
        var result = new double[3];
        foreach (var l in lines)
        {
            var head = l.Head;
            if (head.Length == 0) continue;
            if (!int.TryParse(head[^1..], out var phase)) continue;
            if (phase < 1 || phase > 3) continue;
            if (l.TryDouble(out var v)) result[phase - 1] = v;
        }
        return result;
    }

    public async Task<double> ReadFrequencyAsync(CancellationToken ct = default)
    {
        var lines = await SendAsync("FRQ", ct);
        foreach (var l in lines)
            if (l.TryDouble(out var v)) return v;
        return double.NaN;
    }

    // ---- Measurement readback (?<ResNr>) ----
    // Reply format per spec is "E<code><v1>,<v2>,<v3>" where <code> is:
    //   @ = Current (?1), A = Voltage (?2), I = THDI (?10), J = THDU (?11),
    //   K = PhiI (?12), L = PhiU (?13)
    // In MODE 1 the line is framed as "?<n>=E<code>v1,v2,v3"; in MODE 0 it's bare.
    public Task<double[]> ReadMeasuredCurrentsAsync(CancellationToken ct = default)
        => ReadResultAsync(1, '@', ct);
    public Task<double[]> ReadMeasuredVoltagesAsync(CancellationToken ct = default)
        => ReadResultAsync(2, 'A', ct);
    public Task<double[]> ReadThdCurrentAsync(CancellationToken ct = default)
        => ReadResultAsync(10, 'I', ct);
    public Task<double[]> ReadThdVoltageAsync(CancellationToken ct = default)
        => ReadResultAsync(11, 'J', ct);
    public Task<double[]> ReadPhiCurrentAsync(CancellationToken ct = default)
        => ReadResultAsync(12, 'K', ct);
    public Task<double[]> ReadPhiVoltageAsync(CancellationToken ct = default)
        => ReadResultAsync(13, 'L', ct);

    private async Task<double[]> ReadResultAsync(int resNr, char code, CancellationToken ct)
    {
        var lines = await SendAsync($"?{resNr}", ct);
        foreach (var l in lines)
        {
            // Body may be "E@,1.2,3.4,5.6" (this firmware) or "E@1.2,3.4,5.6" (spec example)
            // or just the bare values in MODE 0. Normalise by stripping prefix + leading commas.
            var body = l.Body;
            if (body.Length > 1 && body[0] == 'E') body = body[1..];
            if (body.Length > 0 && body[0] == code) body = body[1..];
            body = body.TrimStart(',', ' ');
            var parts = body.Split(',');
            // Take the last three numeric fields (defends against any extra leading delimiter).
            if (parts.Length < 3) continue;
            var slice = parts.Length == 3 ? parts : parts[^3..];
            var result = new double[3];
            var ok = true;
            for (int i = 0; i < 3; i++)
                ok &= double.TryParse(slice[i].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out result[i]);
            if (ok) return result;
        }
        return new double[3]; // zeros if parse fails
    }

    public Task SetMeasurementTimeBaseAsync(double seconds, CancellationToken ct = default)
        => SendAsync($"T{seconds.ToString(CultureInfo.InvariantCulture)}", ct);

    // ---- Harmonics (OWI / OWU) ----
    /// <summary>Set one current harmonic on phase. index 2..31, amp = % of fundamental, phi in degrees.</summary>
    public Task SetCurrentHarmonicAsync(int phase, int index, double amplitudePercent, double phiDeg, CancellationToken ct = default)
        => SendAsync($"OWI{phase},{index},{amplitudePercent.ToString(CultureInfo.InvariantCulture)},{phiDeg.ToString(CultureInfo.InvariantCulture)}", ct);

    /// <summary>Set one voltage harmonic on phase. index 2..31, amp = % of fundamental, phi in degrees.</summary>
    public Task SetVoltageHarmonicAsync(int phase, int index, double amplitudePercent, double phiDeg, CancellationToken ct = default)
        => SendAsync($"OWU{phase},{index},{amplitudePercent.ToString(CultureInfo.InvariantCulture)},{phiDeg.ToString(CultureInfo.InvariantCulture)}", ct);

    /// <summary>Erase all harmonics 2..31 on the given phase (current side). Per spec: "OWI&lt;Phase&gt;,0,0".</summary>
    public Task ClearCurrentHarmonicsAsync(int phase, CancellationToken ct = default)
        => SendAsync($"OWI{phase},0,0", ct);

    /// <summary>Erase all harmonics 2..31 on the given phase (voltage side). Per spec: "OWU&lt;Phase&gt;,0,0".</summary>
    public Task ClearVoltageHarmonicsAsync(int phase, CancellationToken ct = default)
        => SendAsync($"OWU{phase},0,0", ct);

    // ---- Ripple control telegram (RCSI / RCSU / RCSIP / RCSUP) ----
    /// <summary>Append one pulse to the current-side ripple telegram. Amp is in amperes.</summary>
    public Task AddCurrentRipplePulseAsync(int phase, double durationMs, double amplitudeA, double frequencyHz, CancellationToken ct = default)
        => SendAsync($"RCSI{phase},{Inv(durationMs)},{Inv(amplitudeA)},{Inv(frequencyHz)}", ct);

    /// <summary>Append one pulse to the voltage-side ripple telegram. Amp is in volts.</summary>
    public Task AddVoltageRipplePulseAsync(int phase, double durationMs, double amplitudeV, double frequencyHz, CancellationToken ct = default)
        => SendAsync($"RCSU{phase},{Inv(durationMs)},{Inv(amplitudeV)},{Inv(frequencyHz)}", ct);

    /// <summary>Append one pulse to the current-side ripple telegram. Amp is in percent of fundamental.</summary>
    public Task AddCurrentRipplePulsePercentAsync(int phase, double durationMs, double amplitudePercent, double frequencyHz, CancellationToken ct = default)
        => SendAsync($"RCSIP{phase},{Inv(durationMs)},{Inv(amplitudePercent)},{Inv(frequencyHz)}", ct);

    /// <summary>Append one pulse to the voltage-side ripple telegram. Amp is in percent of fundamental.</summary>
    public Task AddVoltageRipplePulsePercentAsync(int phase, double durationMs, double amplitudePercent, double frequencyHz, CancellationToken ct = default)
        => SendAsync($"RCSUP{phase},{Inv(durationMs)},{Inv(amplitudePercent)},{Inv(frequencyHz)}", ct);

    /// <summary>Stop a running ripple test on the current side and erase its table.</summary>
    public Task StopCurrentRippleAsync(int phase, CancellationToken ct = default)
        => SendAsync($"RCSI{phase},0", ct);

    /// <summary>Stop a running ripple test on the voltage side and erase its table.</summary>
    public Task StopVoltageRippleAsync(int phase, CancellationToken ct = default)
        => SendAsync($"RCSU{phase},0", ct);

    /// <summary>Read ripple status on the current side: 0 = idle, 000.0 = ready, 0.1..100 = running %.</summary>
    public async Task<double> ReadCurrentRippleStatusAsync(int phase, CancellationToken ct = default)
    {
        var lines = await SendAsync($"RCSI{phase}", ct);
        foreach (var l in lines) if (l.TryDouble(out var v)) return v;
        return 0;
    }

    public async Task<double> ReadVoltageRippleStatusAsync(int phase, CancellationToken ct = default)
    {
        var lines = await SendAsync($"RCSU{phase}", ct);
        foreach (var l in lines) if (l.TryDouble(out var v)) return v;
        return 0;
    }

    // ---- Big/small current connector relay (SKLI — PPS 400.3 only) ----
    /// <summary>Steer the relay connecting the high-current and standard current connectors. flag: 0 = open, 1 = closed.</summary>
    public Task SetCurrentConnectorRelayAsync(int phase, int flag, CancellationToken ct = default)
        => SendAsync($"SKLI{phase},{flag}", ct);

    private static string Inv(double d) => d.ToString(CultureInfo.InvariantCulture);

    // ---- Setpoint mutators ----

    public Task SetVoltageAsync(int phase, double volts, CancellationToken ct = default)
        => SendAsync($"U{phase},{volts.ToString(CultureInfo.InvariantCulture)}", ct);

    public Task SetCurrentAsync(int phase, double amps, CancellationToken ct = default)
        => SendAsync($"I{phase},{amps.ToString(CultureInfo.InvariantCulture)}", ct);

    public Task SetVoltagePhaseAsync(int phase, double degrees, CancellationToken ct = default)
        => SendAsync($"PH{phase},{degrees.ToString(CultureInfo.InvariantCulture)}", ct);

    public Task SetPhiAsync(int phase, double degrees, CancellationToken ct = default)
        => SendAsync($"W{phase},{degrees.ToString(CultureInfo.InvariantCulture)}", ct);

    public Task SetFrequencyAsync(double hz, CancellationToken ct = default)
        => SendAsync($"FRQ{hz.ToString(CultureInfo.InvariantCulture)}", ct);

    public Task SetVoltageRangeAsync(int phase, int range, CancellationToken ct = default)
        => SendAsync($"BU{phase},{range}", ct);

    public Task SetCurrentRangeAsync(int phase, int range, CancellationToken ct = default)
        => SendAsync($"BI{phase},{range}", ct);

    public Task SetVoltageRampAsync(int phase, double seconds, CancellationToken ct = default)
        => SendAsync($"RAMPU{phase},{seconds.ToString(CultureInfo.InvariantCulture)}", ct);

    public Task SetCurrentRampAsync(int phase, double seconds, CancellationToken ct = default)
        => SendAsync($"RAMPI{phase},{seconds.ToString(CultureInfo.InvariantCulture)}", ct);

    /// <summary>Apply previously-defined setpoints. Caller should poll IsBusyAsync after.</summary>
    public Task ApplyAsync(CancellationToken ct = default) => SendAsync("SET", ct);

    /// <summary>Switch off generators. Default = ramped both U and I.</summary>
    public Task OffAsync(OffMode mode = OffMode.RampedAll, CancellationToken ct = default)
        => SendAsync($"OFF{(int)mode}", ct);

    /// <summary>Switch on generators with the values that were active before the last OFF.</summary>
    public Task OnAsync(OnMode mode = OnMode.All, CancellationToken ct = default)
        => SendAsync($"ON{(int)mode}", ct);

    /// <summary>EMERGENCY OFF — immediate kill of both U and I amplifiers (no ramp).</summary>
    public Task EmergencyOffAsync(CancellationToken ct = default) => SendAsync("OFF1", ct);

    public async ValueTask DisposeAsync()
    {
        try { if (_port.IsOpen) _port.Close(); } catch { /* swallow on shutdown */ }
        _port.Dispose();
        _mutex.Dispose();
        await Task.CompletedTask;
    }
}

public enum OffMode { RampedAll = 0, ImmediateAll = 1, ImmediateU = 2, ImmediateI = 3 }
public enum OnMode  { All = 1, UOnly = 2, IOnly = 3 }
