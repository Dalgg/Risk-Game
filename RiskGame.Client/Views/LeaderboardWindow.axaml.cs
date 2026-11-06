using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.Exceptions;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class LeaderboardWindow : Window
    {
        private readonly LeaderboardViewModel leaderboardViewModel;

        public LeaderboardWindow()
        {
            InitializeComponent();

            leaderboardViewModel = new LeaderboardViewModel();
            DataContext = leaderboardViewModel;

            try
            {
                leaderboardViewModel.LoadGlobalScores();
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not load global scores upon opening leaderboard window.");
            }
        }

        private void OnSortClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            try
            {
                leaderboardViewModel.ToggleSortDirection();
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not toggle sort direction in leaderboard window.");
            }
        }

        private void OnGlobalClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            try
            {
                leaderboardViewModel.LoadGlobalScores();
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not reload global scores in leaderboard window.");
            }
        }

        private void BackClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (Application.Current != null && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
        }
    }
}