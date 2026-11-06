using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class RoomsWindow : Window
    {
        public RoomsWindow()
        {
            InitializeComponent();
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
                return;
            }

            LobbyViewModel lobbyViewModel = new LobbyViewModel();
            LobbyDetails lobbyDetails;

            try
            {
                lobbyDetails = lobbyViewModel.JoinLobby(enteredRoomCode);
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                ShowJoinError(lobbyNotFoundException.Message);
                return;
            }
            catch (LobbyFullException lobbyFullException)
            {
                ShowJoinError(lobbyFullException.Message);
                return;
            }
            catch (DataOperationException dataOperationException)
            {
                ShowJoinError(dataOperationException.Message);
                return;
            }

            if (Application.Current != null &&
                Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                LobbyWindow lobbyWindow = new LobbyWindow(lobbyDetails, lobbyViewModel);
                desktopLifetime.MainWindow = lobbyWindow;
                lobbyWindow.Show();
                Close();
            }
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
