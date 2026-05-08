using System.Windows.Controls;

namespace MtePpsControl.Views;

public partial class HarmonicsView : UserControl
{
    public HarmonicsView()
    {
        InitializeComponent();
        this.AttachWhenLoaded(m => m.Harmonics);
    }
}
