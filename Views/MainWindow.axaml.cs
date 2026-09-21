using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Risk_Game.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Show();
    }

    private void OnLoginClick(object? sender, RoutedEventArgs e)
    {
        var loginWindow = new LoginWindow();
        loginWindow.Show();
    }

    private void OnRoomsClick(object? sender, RoutedEventArgs e)
    {
        var roomsWindow = new RoomsWindow();
        roomsWindow.Show();
    }
}
