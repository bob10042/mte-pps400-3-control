using System.IO.Ports;
using System.Text;

namespace MtePpsControl.Protocol;

/// <summary>
/// Probes every available COM port at 19200 8N1 with a VER request and returns
/// the first one whose reply contains "PPS". Designed to make the app portable
/// across laptops without hard-coding COM5.
/// </summary>
public static class PpsAutoDetect
{
    public sealed record DetectionResult(string PortName, string VersionLine);

    public static async Task<DetectionResult?> ProbeAsync(string? preferredPort = null,
                                                          CancellationToken ct = default)
    {
        var available = SerialPort.GetPortNames().Distinct().OrderBy(s => s).ToList();
        // Try preferred (e.g. last-good) first, then the rest
        var ordered = preferredPort != null && available.Contains(preferredPort, StringComparer.OrdinalIgnoreCase)
            ? new[] { preferredPort }.Concat(available.Where(p => !p.Equals(preferredPort, StringComparison.OrdinalIgnoreCase)))
            : available;

        foreach (var port in ordered)
        {
            ct.ThrowIfCancellationRequested();
            var hit = await TryProbeAsync(port, ct);
            if (hit != null) return hit;
        }
        return null;
    }

    private static async Task<DetectionResult?> TryProbeAsync(string port, CancellationToken ct)
    {
        SerialPort? sp = null;
        try
        {
            sp = new SerialPort(port, 19200, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                DtrEnable = false,
                RtsEnable = false,
                ReadTimeout = 600,
                WriteTimeout = 600,
                NewLine = "\r",
                Encoding = Encoding.ASCII,
            };
            sp.Open();
            sp.DiscardInBuffer();
            sp.DiscardOutBuffer();
            sp.Write("VER\r");

            // Allow up to ~700 ms for a reply, then read whatever's there
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(700);
            var buf = new byte[256];
            var sb = new StringBuilder();
            while (DateTime.UtcNow < deadline)
            {
                if (sp.BytesToRead > 0)
                {
                    var n = await sp.BaseStream.ReadAsync(buf, ct);
                    if (n > 0) sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                }
                else
                {
                    await Task.Delay(40, ct);
                }
            }
            var reply = sb.ToString();
            if (reply.Contains("PPS", StringComparison.OrdinalIgnoreCase))
            {
                return new DetectionResult(port, reply.Replace('\r', ' ').Trim());
            }
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // port unavailable / busy / wrong baud — skip
        }
        finally
        {
            try { sp?.Close(); sp?.Dispose(); } catch { /* swallow */ }
        }
        return null;
    }
}
