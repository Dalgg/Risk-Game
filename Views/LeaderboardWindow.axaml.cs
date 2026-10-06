using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.Exceptions;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class LeaderboardWindow : Window
    {
        private readonly LeaderboardViewModel _viewModel;

        public LeaderboardWindow()
        {
            InitializeComponent();

            _viewModel = new LeaderboardViewModel();
            DataContext = _viewModel;
        
            try
            {
                try
            {
                _viewModel.LoadGlobalScores();
            }
            catch (DataOperationException)
            {
            }
            }
            catch (DataOperationException)
            {
                // In a real app we would show a dialog. For now we prevent crash.
            }
        }

        private void OnSortClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.ToggleSortDirection();
            }
            catch (DataOperationException)
            {
            }
        }

        private void OnGlobalClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.LoadGlobalScores();
            }
            catch (DataOperationException)
            {
            }
        }

        private void OnBackClick(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
        }
    }
}