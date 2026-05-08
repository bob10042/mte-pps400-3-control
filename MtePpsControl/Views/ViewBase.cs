using System.Windows;
using System.Windows.Controls;
using MtePpsControl.ViewModels;

namespace MtePpsControl.Views;

/// <summary>
/// Sub-views are instantiated as Style.Setter values, so their UserControl-attribute
/// DataContext bindings (with RelativeSource AncestorType=Window) silently fail at
/// parse time before the parent is in the visual tree. This helper attaches the
/// correct child viewmodel as DataContext after the view is loaded into the tree.
/// </summary>
public static class ViewDataContextHelper
{
    public static void AttachWhenLoaded<TViewModel>(this UserControl view, Func<MainViewModel, TViewModel> selector)
        where TViewModel : class
    {
        view.Loaded += (_, _) =>
        {
            if (view.DataContext is TViewModel) return;
            var wnd = Window.GetWindow(view);
            if (wnd?.DataContext is MainViewModel mvm)
                view.DataContext = selector(mvm);
        };
    }
}
