namespace MtePpsControl.ViewModels;

public sealed class SequenceStep : ObservableObject
{
    private string _name = "Step";
    public string Name { get => _name; set => Set(ref _name, value); }

    public double[] U { get; set; } = new double[3];
    public double[] I { get; set; } = new double[3];
    public double[] PH { get; set; } = new double[] { 0, 120, 240 };
    public double[] Phi { get; set; } = new double[3];

    private double _frequency = 50;
    public double Frequency { get => _frequency; set => Set(ref _frequency, value); }

    private double _holdSeconds = 5;
    public double HoldSeconds { get => _holdSeconds; set => Set(ref _holdSeconds, value); }

    public string Summary =>
        $"{Name}: U=[{U[0]:F1},{U[1]:F1},{U[2]:F1}]V  I=[{I[0]:F2},{I[1]:F2},{I[2]:F2}]A  {Frequency:F1}Hz  {HoldSeconds:F0}s";
}
