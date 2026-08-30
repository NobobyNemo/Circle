using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Circle.Desktop.ViewModels;

namespace Circle.Desktop.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private async void ChooseChordLibraryFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку с аккордами",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is not null)
            vm.SongsViewModel.SetLibraryRoot(folder.Path.LocalPath);
    }
}
