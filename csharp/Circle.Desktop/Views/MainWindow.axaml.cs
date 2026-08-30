using Avalonia.Controls;
using Circle.Desktop.ViewModels;

namespace Circle.Desktop.Views;

public partial class MainWindow : Window
{
    private SettingsWindow? _settingsWindow;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        if (DataContext is MainViewModel vm)
        {
            vm.SettingsRequested += OnSettingsRequested;
            await vm.SongsViewModel.InitializeLibraryAsync(StorageProvider);
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SaveWorkspace();
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow
        {
            DataContext = DataContext,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show(this);
    }
}
