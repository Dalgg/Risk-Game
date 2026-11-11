using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class InboxWindow : Window
    {
        public InboxWindow()
        {
            InitializeComponent();
            SessionUser? currentSessionUser = SessionService.CurrentUser;
            if (currentSessionUser != null && !currentSessionUser.IsGuest)
            {
                InboxViewModel inboxViewModel = new InboxViewModel(currentSessionUser);
                inboxViewModel.LobbyJoined += OnLobbyJoined;
                DataContext = inboxViewModel;
            }
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs routedEventArgs)
        {
            RefreshButton.Focus();
            if (DataContext is InboxViewModel inboxViewModel)
            {
                inboxViewModel.RefreshCommand.Execute(null);
            }
        }

        private void OnBackClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (Application.Current != null
                && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
        }

        private void OnLobbyJoined(object? sender, LobbyJoinedEventArgs lobbyJoinedEventArgs)
        {
            if (Application.Current != null
                && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                LobbyWindow lobbyWindow = new LobbyWindow(lobbyJoinedEventArgs.LobbyDetails, new LobbyViewModel());
                desktopLifetime.MainWindow = lobbyWindow;
                lobbyWindow.Show();
                Close();
            }
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            if (DataContext is InboxViewModel inboxViewModel)
            {
                inboxViewModel.LobbyJoined -= OnLobbyJoined;
            }
            base.OnClosed(eventArgs);
        }
    }
}
