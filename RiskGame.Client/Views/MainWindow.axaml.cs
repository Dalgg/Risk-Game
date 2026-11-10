using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.ViewModels;
using RiskGame.Services;

namespace RiskGame.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void ChangeWindow(Window newWindow)
        {
            if (Application.Current != null
            && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.MainWindow = newWindow;
                newWindow.Show();
                Close();
            }
        }

        private void OnSettingsClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            ChangeWindow(new SettingsWindow());
        }

        private void OnFriendsClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            SessionUser? currentSessionUser = SessionService.CurrentUser;
            if (currentSessionUser == null || currentSessionUser.IsGuest)
            {
                ChangeWindow(new LoginWindow());
                return;
            }
            ChangeWindow(new FriendsWindow());
        }

        private void OnLoginClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            ChangeWindow(new LoginWindow());
        }

        private void OnRoomsClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            ChangeWindow(new RoomsWindow());
        }

        private void OnLeaderboardClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            ChangeWindow(new LeaderboardWindow());
        }

        private void OnExitClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            Close();
        }

    }
}
