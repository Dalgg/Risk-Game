using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using RiskGame.ViewModels;
using RiskGame.Views;

namespace RiskGame.Services
{
    public sealed class LeaderboardNavigationService
    {
        public void OpenLeaderboard()
        {
            SessionUser? session = SessionService.CurrentUser;
            bool isRegistered = session != null && !session.IsGuest;
            if (isRegistered && session != null)
            {
                LeaderboardViewModel leaderboardViewModel = new LeaderboardViewModel(session, this);
                LeaderboardWindow leaderboardWindow = new LeaderboardWindow { DataContext = leaderboardViewModel };
                ChangeWindow(leaderboardWindow);
                leaderboardViewModel.RefreshCommand.Execute(null);
            }
            else
            {
                OpenLogin();
            }
        }

        public void OpenLogin()
        {
            ChangeWindow(new LoginWindow());
        }

        public void ReturnToMenu()
        {
            ChangeWindow(new MainWindow());
        }

        private void ChangeWindow(Window window)
        {
            if (Application.Current != null)
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                {
                    Window? previousWindow = lifetime.MainWindow;
                    lifetime.MainWindow = window;
                    window.Show();
                    if (previousWindow != null)
                    {
                        previousWindow.Close();
                    }
                }
            }
        }
    }
}
