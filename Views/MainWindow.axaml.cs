using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RiskGame.Views
{

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnSettingsClick(object? sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.Show();
        }

        private void OnLoginClick(object? sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
        }

        private void OnRoomsClick(object? sender, RoutedEventArgs e)
        {
            RoomsWindow roomsWindow = new RoomsWindow();
            roomsWindow.Show();
        }
    }

}
