using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.Views;

namespace Risk_Game.Views;

public partial class LeaderboardWindow : Window
{
    public LeaderboardWindow()
    {
        InitializeComponent();
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            this.Close();
        }
    }
}