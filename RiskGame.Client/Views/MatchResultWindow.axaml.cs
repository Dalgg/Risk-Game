using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RiskGame.Contracts.Lobby;
using RiskGame.Contracts.Match;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class MatchResultWindow : Window
    {
        private readonly MatchResultViewModel resultViewModel = new MatchResultViewModel();
        private int matchId;
        private bool hasReturned;
        private bool hasClosed;

        public MatchResultWindow()
        {
            InitializeComponent();
        }

        public MatchResultWindow(BoardState state) : this()
        {
            matchId = state.MatchId;
            BoardPlayer? winner = state.Players.FirstOrDefault(player => player.UserId == state.WinnerUserId);
            WinnerTextBlock.Text = Strings.MatchCancelledNoWinner;
            if (winner != null)
            {
                string winnerFormat = Strings.MatchWinnerFormat;
                if (state.EndReason == MatchEndReason.Abandonment)
                {
                    winnerFormat = Strings.MatchWinnerByAbandonmentFormat;
                }
                WinnerTextBlock.Text = string.Format(winnerFormat, winner.Nickname);
            }
            ConfigureReturnOptions(state);
        }

        private void ConfigureReturnOptions(BoardState state)
        {
            if (HasCurrentPlayerAbandoned(state))
            {
                ReturnButton.IsVisible = false;
                ReturnButton.IsEnabled = false;
                MenuButton.IsVisible = true;
                ErrorTextBlock.Text = Strings.MatchResultAbandonedNotice;
            }
            else if (state.EndReason == MatchEndReason.Cancelled)
            {
                MenuButton.IsVisible = true;
            }
        }

        private static bool HasCurrentPlayerAbandoned(BoardState state)
        {
            int currentUserId = SessionService.GetOrCreateCurrentUser().IdUser;
            BoardPlayer? currentPlayer = state.Players.FirstOrDefault(player => player.UserId == currentUserId);
            return currentPlayer != null && currentPlayer.HasAbandoned;
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            hasClosed = true;
            if (!hasReturned)
            {
                Task.Run(resultViewModel.LeaveLobby);
            }
            base.OnClosed(eventArgs);
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (ReturnButton.IsVisible)
            {
                ReturnButton.Focus(NavigationMethod.Tab);
            }
            else
            {
                MenuButton.Focus(NavigationMethod.Tab);
            }
        }

        private void ReturnClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (!hasClosed && ReturnButton.IsEnabled)
            {
                ReturnButton.IsEnabled = false;
                MenuButton.IsEnabled = false;
                ErrorTextBlock.Text = string.Empty;
                Task.Run(ReturnToLobby);
            }
        }

        private void ReturnToLobby()
        {
            try
            {
                LobbyDetails details = resultViewModel.ReturnToLobby(matchId);
                Dispatcher.UIThread.Post(() => OpenLobby(details));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not open the original lobby from the result window.");
                Dispatcher.UIThread.Post(() => ShowError(dataOperationException.Message));
            }
        }

        private void OpenLobby(LobbyDetails details)
        {
            IClassicDesktopStyleApplicationLifetime? desktopLifetime = ResolveDesktopLifetime();
            if (!hasClosed && desktopLifetime != null)
            {
                LobbyWindow lobby = new LobbyWindow(details, new LobbyViewModel());
                hasReturned = true;
                desktopLifetime.MainWindow = lobby;
                lobby.Show();
                Close();
            }
        }

        private static IClassicDesktopStyleApplicationLifetime? ResolveDesktopLifetime()
        {
            IClassicDesktopStyleApplicationLifetime? desktopLifetime = null;
            if (Application.Current != null
                && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                desktopLifetime = lifetime;
            }
            return desktopLifetime;
        }

        private void ShowError(string message)
        {
            if (!hasClosed)
            {
                ErrorTextBlock.Text = message;
                ReturnButton.IsEnabled = true;
                MenuButton.IsVisible = true;
                MenuButton.IsEnabled = true;
            }
        }

        private void MenuClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (!hasClosed && MenuButton.IsEnabled)
            {
                ReturnButton.IsEnabled = false;
                MenuButton.IsEnabled = false;
                Task.Run(LeaveForMenu);
            }
        }

        private void LeaveForMenu()
        {
            resultViewModel.LeaveLobby();
            Dispatcher.UIThread.Post(OpenMenu);
        }

        private void OpenMenu()
        {
            IClassicDesktopStyleApplicationLifetime? lifetime = ResolveDesktopLifetime();
            if (!hasClosed && lifetime != null)
            {
                MainWindow menu = new MainWindow();
                hasReturned = true;
                lifetime.MainWindow = menu;
                menu.Show();
                Close();
            }
        }
    }
}
