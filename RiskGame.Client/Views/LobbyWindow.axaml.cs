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
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class LobbyWindow : Window, ILobbyCallback
    {
        private static readonly IBrush PlayerCardBackground = new SolidColorBrush(Color.Parse("#25293A"));
        private static readonly IBrush HostBadgeBrush = new SolidColorBrush(Color.Parse("#D4A32D"));
        private static readonly IBrush PlayerNameBrush = Brushes.White;
        private readonly LobbyViewModel _lobbyViewModel;
        private bool _hasLeftLobby;

        public LobbyWindow()
        {
            InitializeComponent();
            _lobbyViewModel = new LobbyViewModel();
        }

        public LobbyWindow(LobbyDetails initialDetails, LobbyViewModel lobbyViewModel) : this()
        {
            _lobbyViewModel = lobbyViewModel;
            ApplyLobbyDetails(initialDetails);
            _lobbyViewModel.SubscribeToEvents(this);
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

        protected override void OnClosed(EventArgs e)
        {
            EnsureLobbyLeft();
            base.OnClosed(e);
        }

        private void ApplyLobbyDetails(LobbyDetails details)
        {
            RoomCodeTextBlock.Text = $"ID DE LA SALA: #{details.LobbyCode}";
            RenderPlayers(details.Players);
        }

        private void RenderPlayers(IReadOnlyList<LobbyPlayer> players)
        {
            PlayersStackPanel.Children.Clear();

            foreach (LobbyPlayer lobbyPlayer in players)
            {
                Border playerCard = CreatePlayerCard(lobbyPlayer);
                PlayersStackPanel.Children.Add(playerCard);
            }
        }

        private static Border CreatePlayerCard(LobbyPlayer lobbyPlayer)
        {
            string displayName = lobbyPlayer.Player?.Nickname ?? "Jugador";
            string roleText = lobbyPlayer.IsHost ? "ANFITRIÓN" : "CONECTADO";

            Grid cardGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto")
            };

            TextBlock nameBlock = new TextBlock
            {
                Text = displayName,
                Foreground = PlayerNameBrush,
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameBlock, 0);

            TextBlock roleBlock = new TextBlock
            {
                Text = roleText,
                Foreground = HostBadgeBrush,
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(roleBlock, 1);

            cardGrid.Children.Add(nameBlock);
            cardGrid.Children.Add(roleBlock);

            return new Border
            {
                Background = PlayerCardBackground,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10),
                Child = cardGrid
            };
        }

        private void OnLeaveLobbyClick(object? sender, RoutedEventArgs e)
        {
            EnsureLobbyLeft();
            ReturnToRoomsWindow();
        }

        private void EnsureLobbyLeft()
        {
            if (!_hasLeftLobby)
            {
                _hasLeftLobby = true;
                _lobbyViewModel.LeaveLobby();
            }
        }

        private void ReturnToRoomsWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                RoomsWindow roomsWindow = new RoomsWindow();
                desktopLifetime.MainWindow = roomsWindow;
                roomsWindow.Show();
                Close();
            }
        }
    }
}
