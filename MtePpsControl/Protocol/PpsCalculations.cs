namespace MtePpsControl.Protocol;

/// <summary>
/// The PPS 400.3 source reports U, I, THDI, THDU, PhiI and PhiU but does NOT directly
/// report active / reactive / apparent power or power factor. We derive those from the
/// measurements the unit *does* return. Single-phase math; the caller sums for 3-phase totals.
/// </summary>
public static class PpsCalculations
{
    /// <summary>RMS apparent power S = U·I (VA).</summary>
    public static double ApparentPower(double uRms, double iRms) => uRms * iRms;

    /// <summary>Active power P = U·I·cos(φ) (W).</summary>
    public static double ActivePower(double uRms, double iRms, double phiDeg)
        => uRms * iRms * Math.Cos(phiDeg * Math.PI / 180.0);

    /// <summary>Reactive power Q = U·I·sin(φ) (VAr).</summary>
    public static double ReactivePower(double uRms, double iRms, double phiDeg)
        => uRms * iRms * Math.Sin(phiDeg * Math.PI / 180.0);

    /// <summary>Power factor PF = cos(φ) (signed: leading negative, lagging positive).</summary>
    public static double PowerFactor(double phiDeg)
        => Math.Cos(phiDeg * Math.PI / 180.0);

    /// <summary>Returns P, Q, S, PF in one go.</summary>
    public static (double P, double Q, double S, double PF) PerPhase(double uRms, double iRms, double phiDeg)
    {
        var s  = ApparentPower(uRms, iRms);
        var p  = ActivePower(uRms, iRms, phiDeg);
        var q  = ReactivePower(uRms, iRms, phiDeg);
        var pf = PowerFactor(phiDeg);
        return (p, q, s, pf);
    }

    /// <summary>3-phase totals from per-phase arrays of length 3.</summary>
    public static (double Ptot, double Qtot, double Stot, double PFavg) ThreePhaseTotal(
        double[] u, double[] i, double[] phiDeg)
    {
        if (u.Length < 3 || i.Length < 3 || phiDeg.Length < 3)
            return (0, 0, 0, 0);
        double p = 0, q = 0, s = 0;
        for (int n = 0; n < 3; n++)
        {
            var ph = PerPhase(u[n], i[n], phiDeg[n]);
            p += ph.P; q += ph.Q; s += ph.S;
        }
        // Average PF based on Ptot / Stot, signed by Q sign
        double pfAvg = s > 0 ? p / s : 0;
        return (p, q, s, pfAvg);
    }
}
