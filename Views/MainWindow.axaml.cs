using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.ViewModels;

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
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.MainWindow = newWindow;
                newWindow.Show();
                Close();
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
            Close();
        }

        private void OnProfileClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Logout();
            }
        }
    }
}