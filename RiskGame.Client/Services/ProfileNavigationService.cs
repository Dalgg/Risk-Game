using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using RiskGame.ViewModels;
using RiskGame.Views;

namespace RiskGame.Services
{
    public sealed class ProfileNavigationService
    {
        public void OpenProfile()
        {
            SessionUser? session = SessionService.CurrentUser;
            if (session != null && !session.IsGuest)
            {
                ProfileViewModel profileViewModel = new ProfileViewModel(session);
                ProfileWindow window = new ProfileWindow { DataContext = profileViewModel };
                ChangeWindow(window);
                Button? refreshButton = window.FindControl<Button>("RefreshButton");
                if (refreshButton != null)
                {
                    refreshButton.Focus();
                }

                profileViewModel.RetryCommand.Execute(null);
            }
        }

        public void ReturnToMenu()
        {
            ChangeWindow(new MainWindow());
        }

        private void ChangeWindow(Window window)
        {
            if (Application.Current != null
                && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
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
