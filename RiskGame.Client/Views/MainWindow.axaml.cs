using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class MainWindow : Window
    {
        private bool isClosed;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            SessionService.InboxChanged += OnInboxChanged;
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs routedEventArgs)
        {
            RefreshInboxIndicator();
        }

        private void OnWindowActivated(object? sender, EventArgs eventArgs)
        {
            RefreshInboxIndicator();
        }

        private void OnInboxChanged(object? sender, EventArgs eventArgs)
        {
            Dispatcher.UIThread.Post(RefreshInboxIndicator);
        }

        private void RefreshInboxIndicator()
        {
            if (!isClosed && DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.RefreshInboxNotifications();
            }
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            isClosed = true;
            SessionService.InboxChanged -= OnInboxChanged;
            if (DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.StopInboxNotifications();
            }
            base.OnClosed(eventArgs);
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
            }
            else
            {
                ChangeWindow(new FriendsWindow());
            }
        }

        private void OnInboxClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            SessionUser? currentSessionUser = SessionService.CurrentUser;
            if (currentSessionUser == null || currentSessionUser.IsGuest)
            {
                ChangeWindow(new LoginWindow());
            }
            else
            {
                ChangeWindow(new InboxWindow());
            }
        }

        private void OnLoginClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            SessionService.Logout();
            ChangeWindow(new LoginWindow());
        }

        private void OnRoomsClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            ChangeWindow(new RoomsWindow());
        }

        private static void OnLeaderboardClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            LeaderboardNavigationService navigation = new LeaderboardNavigationService();
            navigation.OpenLeaderboard();
        }

        private void OnExitClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            Close();
        }
    }
}
