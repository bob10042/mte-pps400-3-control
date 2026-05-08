using System.Globalization;
using System.IO;
using MtePpsControl.Protocol;

namespace MtePpsControl.ViewModels;

/// <summary>
/// Excel-friendly CSV logger. Each poll tick writes one row with timestamp + per-phase U/I/THD/φ
/// + computed P/Q/S/PF + 3-phase totals. Toggle on/off; while off no file is held open.
/// </summary>
public sealed class CsvLogger : IDisposable
{
    private StreamWriter? _writer;
    private readonly string _logDir;

    public CsvLogger()
    {
        _logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                               "MtePpsControl", "logs");
        Directory.CreateDirectory(_logDir);
    }

    public bool IsLogging => _writer != null;
    public string? CurrentFilePath { get; private set; }

    public void Start()
    {
        if (_writer != null) return;
        var fname = $"vi-log_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        CurrentFilePath = Path.Combine(_logDir, fname);
        _writer = new StreamWriter(CurrentFilePath, append: false) { AutoFlush = true };
        _writer.WriteLine(string.Join(",",
            "timestamp",
            "U1_V","U2_V","U3_V",
            "I1_A","I2_A","I3_A",
            "PhiU1_deg","PhiU2_deg","PhiU3_deg",
            "PhiI1_deg","PhiI2_deg","PhiI3_deg",
            "THDU1_pct","THDU2_pct","THDU3_pct",
            "THDI1_pct","THDI2_pct","THDI3_pct",
            "P1_W","P2_W","P3_W","Ptot_W",
            "Q1_VAr","Q2_VAr","Q3_VAr","Qtot_VAr",
            "S1_VA","S2_VA","S3_VA","Stot_VA",
            "PF1","PF2","PF3","PFavg",
            "STATU1","STATU2","STATU3",
            "STATI1","STATI2","STATI3"));
    }

    public void Stop()
    {
        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;
        CurrentFilePath = null;
    }

    /// <summary>Append a single tick. Caller passes the latest measurement arrays.</summary>
    public void WriteTick(
        double[] u, double[] i, double[] phiU, double[] phiI,
        double[] thdU, double[] thdI,
        string[] statU, string[] statI)
    {
        if (_writer == null) return;
        var phasePhi = new[] { phiI[0], phiI[1], phiI[2] };  // use absolute Phi I as the angle for power calc
        var p = new double[3]; var q = new double[3]; var s = new double[3]; var pf = new double[3];
        for (int n = 0; n < 3; n++)
        {
            var ph = PpsCalculations.PerPhase(u[n], i[n], phasePhi[n]);
            p[n] = ph.P; q[n] = ph.Q; s[n] = ph.S; pf[n] = ph.PF;
        }
        var (Ptot, Qtot, Stot, PFavg) = PpsCalculations.ThreePhaseTotal(u, i, phasePhi);

        string F(double d) => d.ToString("G6", CultureInfo.InvariantCulture);

        _writer.WriteLine(string.Join(",",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
            F(u[0]), F(u[1]), F(u[2]),
            F(i[0]), F(i[1]), F(i[2]),
            F(phiU[0]), F(phiU[1]), F(phiU[2]),
            F(phiI[0]), F(phiI[1]), F(phiI[2]),
            F(thdU[0]), F(thdU[1]), F(thdU[2]),
            F(thdI[0]), F(thdI[1]), F(thdI[2]),
            F(p[0]), F(p[1]), F(p[2]), F(Ptot),
            F(q[0]), F(q[1]), F(q[2]), F(Qtot),
            F(s[0]), F(s[1]), F(s[2]), F(Stot),
            F(pf[0]), F(pf[1]), F(pf[2]), F(PFavg),
            statU[0], statU[1], statU[2],
            statI[0], statI[1], statI[2]));
    }

    public void Dispose() => Stop();
}
