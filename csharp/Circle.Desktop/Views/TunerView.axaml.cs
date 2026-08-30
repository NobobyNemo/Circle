using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Circle.Desktop.Views;

public partial class TunerView : UserControl
{
    public TunerView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
