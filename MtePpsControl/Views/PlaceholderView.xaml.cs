using System.Windows;
using System.Windows.Controls;

namespace MtePpsControl.Views;

public partial class PlaceholderView : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PlaceholderView), new PropertyMetadata("Coming soon"));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public PlaceholderView() => InitializeComponent();
}
