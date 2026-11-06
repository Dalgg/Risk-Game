using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using RiskGame.Callbacks;
using RiskGame.Contracts.Lobby;
using RiskGame.Resources;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class LobbyWindow : Window, ILobbyCallback
    {
        private const int PlayerNameFontSize = 16;
        private const int PlayerRoleFontSize = 12;
        private const int PlayerCardCornerRadius = 6;
        private const int CardHorizontalPadding = 12;
        private const int CardVerticalPadding = 10;
        private const int NameColumnIndex = 0;
        private const int RoleColumnIndex = 1;
        private static readonly IBrush PlayerCardBackground = new SolidColorBrush(Color.Parse("#25293A"));
        private static readonly IBrush HostBadgeBrush = new SolidColorBrush(Color.Parse("#D4A32D"));
        private static readonly IBrush PlayerNameBrush = Brushes.White;
        private readonly LobbyViewModel lobbyViewModel;
        private bool hasLeftLobby;

        public LobbyWindow()
        {
            InitializeComponent();
            lobbyViewModel = new LobbyViewModel();
        }

        public LobbyWindow(LobbyDetails initialDetails, LobbyViewModel currentLobbyViewModel) : this()
        {
            lobbyViewModel = currentLobbyViewModel;
            ApplyLobbyDetails(initialDetails);
            lobbyViewModel.SubscribeToEvents(this);
        }

        public void LobbyChanged(LobbyChanged notification)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (notification.Lobby != null)
                {
                    ApplyLobbyDetails(notification.Lobby);
                }
            });
        }

        public void LobbyClosed(LobbyClosed notification)
        {
            Dispatcher.UIThread.Post(ReturnToRoomsWindow);
        }

        public void MatchStarting(MatchStarting notification)
        {
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            EnsureLobbyLeft();
            base.OnClosed(eventArgs);
        }

        private void ApplyLobbyDetails(LobbyDetails lobbyDetails)
        {
            RoomCodeTextBlock.Text = string.Format(Strings.LobbyRoomIdFormat, lobbyDetails.LobbyCode);
            RenderPlayers(lobbyDetails.Players);
        }

        private void RenderPlayers(IReadOnlyList<LobbyPlayer> lobbyPlayers)
        {
            PlayersStackPanel.Children.Clear();

            foreach (LobbyPlayer lobbyPlayer in lobbyPlayers)
            {
                Border playerCard = CreatePlayerCard(lobbyPlayer);
                PlayersStackPanel.Children.Add(playerCard);
            }
        }

        private static Border CreatePlayerCard(LobbyPlayer lobbyPlayer)
        {
            string displayName = Strings.LobbyDefaultPlayerName;
            if (lobbyPlayer.Player != null && !string.IsNullOrWhiteSpace(lobbyPlayer.Player.Nickname))
            {
                displayName = lobbyPlayer.Player.Nickname;
            }

            string roleText = Strings.LobbyConnectedRoleLabel;
            if (lobbyPlayer.IsHost)
            {
                roleText = Strings.LobbyHostRoleLabel;
            }

            Grid cardGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto")
            };

            TextBlock nameBlock = new TextBlock
            {
                Text = displayName,
                Foreground = PlayerNameBrush,
                FontSize = PlayerNameFontSize,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameBlock, NameColumnIndex);

            TextBlock roleBlock = new TextBlock
            {
                Text = roleText,
                Foreground = HostBadgeBrush,
                FontSize = PlayerRoleFontSize,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(roleBlock, RoleColumnIndex);

            cardGrid.Children.Add(nameBlock);
            cardGrid.Children.Add(roleBlock);

            return new Border
            {
                Background = PlayerCardBackground,
                CornerRadius = new CornerRadius(PlayerCardCornerRadius),
                Padding = new Thickness(CardHorizontalPadding, CardVerticalPadding),
                Child = cardGrid
            };
        }

        private void LeaveLobbyClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            EnsureLobbyLeft();
            ReturnToRoomsWindow();
        }

        private void EnsureLobbyLeft()
        {
            if (!hasLeftLobby)
            {
                hasLeftLobby = true;
                lobbyViewModel.LeaveLobby();
            }
        }

        private void ReturnToRoomsWindow()
        {
            if (Application.Current != null &&
                Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                RoomsWindow roomsWindow = new RoomsWindow();
                desktopLifetime.MainWindow = roomsWindow;
                roomsWindow.Show();
                Close();
            }
        }
    }
}
