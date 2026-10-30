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

        private void JoinRoomClick(object? sender, RoutedEventArgs e)
        {
            JoinMessageTextBlock.IsVisible = false;
            RoomCodeTextBox.Text = string.Empty;
            MenuButtonsStackPanel.IsVisible = false;
            JoinCodeStackPanel.IsVisible = true;
            RoomCodeTextBox.Focus();
        }

        private void CancelJoinRoomClick(object? sender, RoutedEventArgs e)
        {
            JoinMessageTextBlock.IsVisible = false;
            JoinCodeStackPanel.IsVisible = false;
            MenuButtonsStackPanel.IsVisible = true;
        }

        private void ConfirmJoinRoomClick(object? sender, RoutedEventArgs e)
        {
            string roomCode = (RoomCodeTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                ShowJoinError(Strings.RoomsErrorEmptyCode);
                return;
            }

            LobbyViewModel lobbyViewModel = new LobbyViewModel();
            LobbyDetails lobbyDetails;

            try
            {
                lobbyDetails = lobbyViewModel.JoinLobby(roomCode);
            }
            catch (LobbyNotFoundException ex)
            {
                ShowJoinError(ex.Message);
                return;
            }
            catch (LobbyFullException ex)
            {
                ShowJoinError(ex.Message);
                return;
            }
            catch (DataOperationException ex)
            {
                ShowJoinError(ex.Message);
                return;
            }

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                LobbyWindow lobbyWindow = new LobbyWindow(lobbyDetails, lobbyViewModel);
                desktopLifetime.MainWindow = lobbyWindow;
                lobbyWindow.Show();
                Close();
            }
        }

        private void ShowJoinError(string message)
        {
            JoinMessageTextBlock.Text = message;
            JoinMessageTextBlock.IsVisible = true;
        }

        private void BackClick(object? sender, RoutedEventArgs e)
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
