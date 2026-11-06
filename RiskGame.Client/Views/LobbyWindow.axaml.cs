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
using RiskGame.Contracts.Chat;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class LobbyWindow : Window, ILobbyCallback, IChatCallback
    {
        private const int PlayerNameFontSize = 16;
        private const int PlayerRoleFontSize = 12;
        private const int PlayerCardCornerRadius = 6;
        private const int CardHorizontalPadding = 12;
        private const int CardVerticalPadding = 10;
        private const int NameColumnIndex = 0;
        private const int RoleColumnIndex = 1;
        private const int ChatMessageFontSize = 14;
        private const int MaxChatMessagesDisplayed = 100;
        private const int MinimumPlayersToStart = 2;
        private static readonly IBrush PlayerCardBackground = new SolidColorBrush(Color.Parse("#25293A"));
        private static readonly IBrush HostBadgeBrush = new SolidColorBrush(Color.Parse("#D4A32D"));
        private static readonly IBrush PlayerNameBrush = Brushes.White;
        private static readonly IBrush OwnChatMessageBrush = new SolidColorBrush(Color.Parse("#D4A32D"));
        private static readonly IBrush OtherChatMessageBrush = Brushes.White;
        private static readonly IBrush OnlineBadgeBrush = new SolidColorBrush(Color.Parse("#4CAF50"));
        private static readonly IBrush OfflineBadgeBrush = new SolidColorBrush(Color.Parse("#8A8E9B"));
        private readonly LobbyViewModel lobbyViewModel;
        private readonly List<LobbyPlayer> currentLobbyPlayers = new List<LobbyPlayer>();
        private bool hasLeftLobby;
        private bool hasEnteredMatch;

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
            lobbyViewModel.SubscribeChat(this);
        }

        public void LobbyChanged(LobbyChanged notification)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!hasEnteredMatch && notification.Lobby != null)
                {
                    ApplyLobbyDetails(notification.Lobby);
                }
            });
        }

        public void LobbyClosed(LobbyClosed notification)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!hasEnteredMatch)
                {
                    ReturnToRoomsWindow();
                }
            });
        }

        public void MatchStarting(MatchStarting notification)
        {
            int startedMatchId = notification.MatchId;
            Dispatcher.UIThread.Post(() => OpenMatchWindow(startedMatchId));
        }

        public void MessageReceived(ChatMessage incomingMessage)
        {
            Dispatcher.UIThread.Post(() => AppendChatMessage(incomingMessage));
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            EnsureLobbyLeft();
            base.OnClosed(eventArgs);
        }

        private void ApplyLobbyDetails(LobbyDetails lobbyDetails)
        {
            RoomCodeTextBlock.Text = string.Format(Strings.LobbyRoomIdFormat, lobbyDetails.LobbyCode);

            currentLobbyPlayers.Clear();
            currentLobbyPlayers.AddRange(lobbyDetails.Players);

            RenderPlayers(lobbyDetails.Players);
            RenderFriends();
            UpdatePlayButtonState(lobbyDetails);
        }

        private void UpdatePlayButtonState(LobbyDetails lobbyDetails)
        {
            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            bool isCurrentUserHost = lobbyDetails.HostUserId == currentSessionUser.IdUser;

            LobbyPlayButton.IsVisible = isCurrentUserHost;
            LobbyPlayButton.IsEnabled = isCurrentUserHost && lobbyDetails.Players.Count >= MinimumPlayersToStart;
        }

        private void AppendChatMessage(ChatMessage incomingMessage)
        {
            string senderName = Strings.LobbyDefaultPlayerName;
            bool isOwnMessage = false;

            if (incomingMessage.Sender != null)
            {
                if (!string.IsNullOrWhiteSpace(incomingMessage.Sender.Nickname))
                {
                    senderName = incomingMessage.Sender.Nickname;
                }

                SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
                isOwnMessage = incomingMessage.Sender.UserId == currentSessionUser.IdUser;
            }

            IBrush messageBrush = OtherChatMessageBrush;
            if (isOwnMessage)
            {
                messageBrush = OwnChatMessageBrush;
            }

            TextBlock messageBlock = new TextBlock
            {
                Text = string.Format(Strings.LobbyChatMessageFormat, senderName, incomingMessage.Text),
                Foreground = messageBrush,
                FontSize = ChatMessageFontSize,
                TextWrapping = TextWrapping.Wrap
            };

            ChatMessagesStackPanel.Children.Add(messageBlock);

            if (ChatMessagesStackPanel.Children.Count > MaxChatMessagesDisplayed)
            {
                ChatMessagesStackPanel.Children.RemoveAt(0);
            }

            Dispatcher.UIThread.Post(() => ChatScrollViewer.ScrollToEnd(), DispatcherPriority.Background);
        }

        private void ShowLobbyMessage(string messageText)
        {
            LobbyMessageTextBlock.Text = messageText;
            LobbyMessageTextBlock.IsVisible = true;
        }

        private void OpenMatchWindow(int startedMatchId)
        {
            if (hasEnteredMatch)
            {
                return;
            }

            hasEnteredMatch = true;
            hasLeftLobby = true;
            lobbyViewModel.StopSubscription();
            lobbyViewModel.StopChatSubscription();

            if (Application.Current != null &&
                Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MatchWindow matchWindow = new MatchWindow(startedMatchId, currentLobbyPlayers);
                desktopLifetime.MainWindow = matchWindow;
                matchWindow.Show();
                Close();
            }
        }

        private void PlayClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            LobbyMessageTextBlock.IsVisible = false;

            try
            {
                lobbyViewModel.StartMatch();
            }
            catch (InvalidOperationException invalidOperationException)
            {
                JsonLogger.Error(invalidOperationException, "The match could not be started from the lobby window.");
                ShowLobbyMessage(Strings.RoomsErrorNotEnoughPlayers);
            }
            catch (DataOperationException dataOperationException)
            {
                ShowLobbyMessage(dataOperationException.Message);
            }
        }

        private void SendChatMessageClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            string messageText = string.Empty;
            if (ChatInputTextBox.Text != null)
            {
                messageText = ChatInputTextBox.Text.Trim();
            }

            if (string.IsNullOrWhiteSpace(messageText))
            {
                return;
            }

            LobbyMessageTextBlock.IsVisible = false;

            try
            {
                lobbyViewModel.SendMessage(messageText);
                ChatInputTextBox.Text = string.Empty;
                ChatInputTextBox.Focus();
            }
            catch (DataOperationException dataOperationException)
            {
                ShowLobbyMessage(dataOperationException.Message);
            }
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

        private void RenderFriends()
        {
            FriendsStackPanel.Children.Clear();

            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            if (currentSessionUser.IsGuest)
            {
                TextBlock guestNoticeBlock = new TextBlock
                {
                    Text = Strings.LobbyFriendsGuestDisabled,
                    Foreground = OtherChatMessageBrush,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                FriendsStackPanel.Children.Add(guestNoticeBlock);
                return;
            }

            try
            {
                List<FriendSummary> friends = lobbyViewModel.GetFriends();
                if (friends.Count == 0)
                {
                    TextBlock emptyNoticeBlock = new TextBlock
                    {
                        Text = Strings.LobbyNoFriendsMessage,
                        Foreground = OtherChatMessageBrush,
                        FontSize = 13,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 10, 0, 0)
                    };
                    FriendsStackPanel.Children.Add(emptyNoticeBlock);
                    return;
                }

                foreach (FriendSummary friend in friends)
                {
                    Border friendCard = CreateFriendCard(friend);
                    FriendsStackPanel.Children.Add(friendCard);
                }
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not populate friends in lobby window.");
            }
        }

        private static Border CreateFriendCard(FriendSummary friendSummary)
        {
            string friendDisplayName = Strings.LobbyDefaultPlayerName;
            if (!string.IsNullOrWhiteSpace(friendSummary.Nickname))
            {
                friendDisplayName = friendSummary.Nickname;
            }

            string statusText = Strings.LobbyFriendOfflineStatus;
            IBrush statusBrush = OfflineBadgeBrush;
            if (friendSummary.IsOnline)
            {
                statusText = Strings.LobbyFriendOnlineStatus;
                statusBrush = OnlineBadgeBrush;
            }

            Grid cardGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto")
            };

            TextBlock nameBlock = new TextBlock
            {
                Text = friendDisplayName,
                Foreground = PlayerNameBrush,
                FontSize = PlayerNameFontSize,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameBlock, NameColumnIndex);

            TextBlock statusBlock = new TextBlock
            {
                Text = statusText,
                Foreground = statusBrush,
                FontSize = PlayerRoleFontSize,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(statusBlock, RoleColumnIndex);

            cardGrid.Children.Add(nameBlock);
            cardGrid.Children.Add(statusBlock);

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