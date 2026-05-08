using System.Windows;
using MtePpsControl.ViewModels;

namespace MtePpsControl;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private MainViewModel Vm => (MainViewModel)DataContext;

    private void Icon_Setter_Click(object sender, RoutedEventArgs e)    => Vm.SelectedView = "setter";
    private void Icon_Source_Click(object sender, RoutedEventArgs e)    => Vm.SelectedView = "source";
    private void Icon_Harmonics_Click(object sender, RoutedEventArgs e) => Vm.SelectedView = "harmonics";
    private void Icon_Ripple_Click(object sender, RoutedEventArgs e)    => Vm.SelectedView = "ripple";
    private void Icon_Sequence_Click(object sender, RoutedEventArgs e)  => Vm.SelectedView = "sequence";
    private void Icon_Database_Click(object sender, RoutedEventArgs e)  => Vm.SelectedView = "database";

    private void TopTabReference_Click(object sender, RoutedEventArgs e) => Vm.SelectedView = "database";
    private void TopTabPps_Click(object sender, RoutedEventArgs e)       => Vm.SelectedView = "setter";
    private void TopTabSequence_Click(object sender, RoutedEventArgs e)  => Vm.SelectedView = "sequence";
    private void TopTabDatabase_Click(object sender, RoutedEventArgs e)  => Vm.SelectedView = "database";
}
