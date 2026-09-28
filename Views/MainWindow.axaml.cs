using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Risk_Game.Views;

namespace RiskGame.Views
{

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ChangeWindow(Window newWindow)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = newWindow;
                newWindow.Show();
                this.Close();
            }
        }

        private void OnSettingsClick(object? sender, RoutedEventArgs e)
        {
            ChangeWindow(new SettingsWindow());
        }

        private void OnLoginClick(object? sender, RoutedEventArgs e)
        {
            ChangeWindow(new LoginWindow());
        }

        private void OnRoomsClick(object? sender, RoutedEventArgs e)
        {
            ChangeWindow(new RoomsWindow());
        }

        private void OnLeaderboardClick(object? sender, RoutedEventArgs e)
        {
            ChangeWindow(new LeaderboardWindow());
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
