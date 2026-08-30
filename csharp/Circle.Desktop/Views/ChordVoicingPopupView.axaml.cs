using Avalonia.Controls;
using Circle.Desktop.ViewModels;

namespace Circle.Desktop.Views;

public partial class ChordVoicingPopupView : UserControl
{
    public ChordVoicingPopupView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is ChordVoicingPopupViewModel vm)
            vm.PropertyChanged += (_, _) => Diagram.InvalidateVisual();
    }
}
