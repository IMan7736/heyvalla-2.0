namespace Dhathuru.DataTools.Builders;

public static class Stats
{
    /// <summary>Linear interpolation percentile (p in 0..1) of an unsorted list. Sorts in place.</summary>
    public static double Percentile(List<double> values, double p)
    {
        if (values.Count == 0) throw new ArgumentException("No values.", nameof(values));
        values.Sort();
        if (values.Count == 1) return values[0];
        var pos = p * (values.Count - 1);
        var lo = (int)Math.Floor(pos);
        var hi = (int)Math.Ceiling(pos);
        return values[lo] + (values[hi] - values[lo]) * (pos - lo);
    }

    /// <summary>Ordinary least squares y = a + b x. R2 is 0 when x has no spread.</summary>
    public static (double Intercept, double Slope, double R2) LinearFit(IReadOnlyList<(double X, double Y)> points)
    {
        var n = points.Count;
        if (n < 2) throw new ArgumentException("Need at least two points.", nameof(points));
        var mx = points.Average(p => p.X);
        var my = points.Average(p => p.Y);
        var sxx = points.Sum(p => (p.X - mx) * (p.X - mx));
        var sxy = points.Sum(p => (p.X - mx) * (p.Y - my));
        var syy = points.Sum(p => (p.Y - my) * (p.Y - my));
        if (sxx == 0) return (my, 0, 0);
        var slope = sxy / sxx;
        var intercept = my - slope * mx;
        var r2 = syy == 0 ? 1 : sxy * sxy / (sxx * syy);
        return (intercept, slope, r2);
    }

    /// <summary>Streaming accumulator for the least squares sums (no list of points kept).</summary>
    public sealed class LinearAccumulator
    {
        private long n;
        private double sx, sy, sxx, sxy, syy;

        public int Count => (int)n;

        public void Add(double x, double y)
        {
            n++;
            sx += x;
            sy += y;
            sxx += x * x;
            sxy += x * y;
            syy += y * y;
        }

        public (double Intercept, double Slope, double R2) Fit()
        {
            if (n < 2) throw new InvalidOperationException("Need at least two points.");
            var mx = sx / n;
            var my = sy / n;
            var cxx = sxx - n * mx * mx;
            var cxy = sxy - n * mx * my;
            var cyy = syy - n * my * my;
            if (cxx <= 0) return (my, 0, 0);
            var slope = cxy / cxx;
            return (my - slope * mx, slope, cyy <= 0 ? 1 : cxy * cxy / (cxx * cyy));
        }
    }
}
