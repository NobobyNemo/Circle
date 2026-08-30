using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Circle.Desktop.Models;
using Circle.Desktop.ViewModels;

namespace Circle.Desktop.Views;

public partial class WheelListManagerWindow : Window
{
    private Button? _pendingDeleteItemBtn;
    private DispatcherTimer? _resetTimer;

    public WheelListManagerWindow()
    {
        InitializeComponent();
    }

    private void OnOpenCreatePopup(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WheelOfFortuneViewModel vm)
        {
            vm.CreateListName = string.Empty;
            vm.IsCreateListPopupOpen = true;
        }
    }

    private void OnCancelCreatePopup(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WheelOfFortuneViewModel vm)
            vm.IsCreateListPopupOpen = false;
    }

    private void OnCreateListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is WheelOfFortuneViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.CreateListName))
                vm.CreateListCommand.Execute(null);
        }
    }

    private void OnCancelAddItemPopup(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WheelOfFortuneViewModel vm)
            vm.IsAddItemPopupOpenManager = false;
    }

    private void OnAddItemKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is WheelOfFortuneViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.NewItemPopupText) || !string.IsNullOrEmpty(vm.NewItemPopupImagePath))
                vm.ConfirmAddItemManagerCommand.Execute(null);
        }
    }

    private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string listName)
            return;

        var point = e.GetCurrentPoint(null);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        if (DataContext is WheelOfFortuneViewModel vm)
            vm.LoadListCommand.Execute(listName);
    }

    private void OnDeleteListClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string listName)
            return;

        if (DataContext is WheelOfFortuneViewModel vm)
            vm.DeleteListCommand.Execute(listName);
    }

    private void OnRenameListClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string listName)
            return;

        if (DataContext is WheelOfFortuneViewModel vm)
            vm.StartRenameList(listName);
    }

    private void OnCancelRenamePopup(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WheelOfFortuneViewModel vm)
            vm.IsRenameListPopupOpen = false;
    }

    private void OnConfirmRenamePopup(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WheelOfFortuneViewModel vm && !string.IsNullOrWhiteSpace(vm.RenameListName))
            vm.ConfirmRenameList();
    }

    private void OnRenameListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is WheelOfFortuneViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.RenameListName))
                vm.ConfirmRenameList();
        }
    }

    private void OnDeleteItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not WheelItem item)
            return;

        if (_pendingDeleteItemBtn == btn)
        {
            ResetPendingDelete();
            if (DataContext is WheelOfFortuneViewModel vm)
                vm.RemoveSavedItemCommand.Execute(item);
            return;
        }

        ResetPendingDelete();
        _pendingDeleteItemBtn = btn;
        btn.Content = "✓?";
        btn.Foreground = new SolidColorBrush(Color.Parse("#ef4444"));
        btn.FontWeight = FontWeight.Bold;
        StartResetTimer();
    }

    private void StartResetTimer()
    {
        _resetTimer?.Stop();
        _resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _resetTimer.Tick += (_, _) => ResetPendingDelete();
        _resetTimer.Start();
    }

    private void ResetPendingDelete()
    {
        if (_pendingDeleteItemBtn is not null)
        {
            _pendingDeleteItemBtn.Content = "✕";
            _pendingDeleteItemBtn.Foreground = new SolidColorBrush(Color.Parse("#94a3b8"));
            _pendingDeleteItemBtn.FontWeight = FontWeight.Normal;
            _pendingDeleteItemBtn = null;
        }

        _resetTimer?.Stop();
        _resetTimer = null;
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
