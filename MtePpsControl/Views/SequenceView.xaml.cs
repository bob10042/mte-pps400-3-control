using System.Windows.Controls;

namespace MtePpsControl.Views;

public partial class SequenceView : UserControl
{
    public SequenceView()
    {
        InitializeComponent();
        this.AttachWhenLoaded(m => m.Sequence);
    }
}
