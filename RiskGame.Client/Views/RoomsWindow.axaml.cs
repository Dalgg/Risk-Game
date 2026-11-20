using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class RoomsWindow : Window
    {
        private const int DefaultTurnDurationSeconds = 60;

        public RoomsWindow()
        {
            InitializeComponent();
        }

        private void CreateRoomClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            CreateMessageTextBlock.IsVisible = false;

            LobbyViewModel lobbyViewModel = new LobbyViewModel();

            try
            {
                LobbyDetails lobbyDetails = lobbyViewModel.CreateLobby(RulesVariant.Standard, DefaultTurnDurationSeconds);
                OpenLobbyWindow(lobbyDetails, lobbyViewModel);
            }
            catch (DataOperationException dataOperationException)
            {
                ShowCreateError(dataOperationException.Message);
            }
        }

        private void JoinRoomClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            JoinMessageTextBlock.IsVisible = false;
            RoomCodeTextBox.Text = string.Empty;
            MenuButtonsStackPanel.IsVisible = false;
            JoinCodeStackPanel.IsVisible = true;
            RoomCodeTextBox.Focus();
        }

        private void CancelJoinRoomClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            JoinMessageTextBlock.IsVisible = false;
            JoinCodeStackPanel.IsVisible = false;
            MenuButtonsStackPanel.IsVisible = true;
        }

        private void ConfirmJoinRoomClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            string enteredRoomCode = string.Empty;
            if (RoomCodeTextBox.Text != null)
            {
                enteredRoomCode = RoomCodeTextBox.Text.Trim();
            }

            if (string.IsNullOrWhiteSpace(enteredRoomCode))
            {
                ShowJoinError(Strings.RoomsErrorEmptyCode);
            }
            else
            {
                JoinLobbyByCode(enteredRoomCode);
            }
        }

        private void JoinLobbyByCode(string enteredRoomCode)
        {
            LobbyViewModel lobbyViewModel = new LobbyViewModel();

            try
            {
                LobbyDetails lobbyDetails = lobbyViewModel.JoinLobby(enteredRoomCode);
                OpenLobbyWindow(lobbyDetails, lobbyViewModel);
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                ShowJoinError(lobbyNotFoundException.Message);
            }
            catch (LobbyFullException lobbyFullException)
            {
                ShowJoinError(lobbyFullException.Message);
            }
            catch (DataOperationException dataOperationException)
            {
                ShowJoinError(dataOperationException.Message);
            }
        }

        private void OpenLobbyWindow(LobbyDetails lobbyDetails, LobbyViewModel lobbyViewModel)
        {
            if (Application.Current != null &&
                Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                LobbyWindow lobbyWindow = new LobbyWindow(lobbyDetails, lobbyViewModel);
                desktopLifetime.MainWindow = lobbyWindow;
                lobbyWindow.Show();
                Close();
            }
        }

        private void ShowCreateError(string errorMessage)
        {
            CreateMessageTextBlock.Text = errorMessage;
            CreateMessageTextBlock.IsVisible = true;
        }

        private void ShowJoinError(string errorMessage)
        {
            JoinMessageTextBlock.Text = errorMessage;
            JoinMessageTextBlock.IsVisible = true;
        }

        private void BackClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (Application.Current != null &&
                Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
        }
    }
}