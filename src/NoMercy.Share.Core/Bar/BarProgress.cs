namespace NoMercy.Share.Core.Bar;

internal sealed class BarProgress(Action<double> report) : IProgress<double>
{
    public void Report(double value) => report(value);
}
