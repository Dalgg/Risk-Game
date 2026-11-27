using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Input;
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
        private const int InvitationColumnIndex = 2;
        private const int ChatMessageFontSize = 14;
        private const int FriendsNoticeFontSize = 13;
        private const int FriendsNoticeTopMargin = 10;
        private const int MaxChatMessagesDisplayed = 100;
        private const int MinimumPlayersToStart = 2;
        private const int InvalidUserIdentifier = 0;
        private const int ReportColumnIndex = 2;
        private const int ReportButtonLeftMargin = 8;
        private const int NoMargin = 0;
        private static readonly IBrush PlayerCardBackground = new SolidColorBrush(Color.Parse("#25293A"));
        private static readonly IBrush HostBadgeBrush = new SolidColorBrush(Color.Parse("#D4A32D"));
        private static readonly IBrush PlayerNameBrush = Brushes.White;
        private static readonly IBrush OwnChatMessageBrush = new SolidColorBrush(Color.Parse("#D4A32D"));
        private static readonly IBrush OtherChatMessageBrush = Brushes.White;
        private static readonly IBrush OnlineBadgeBrush = new SolidColorBrush(Color.Parse("#4CAF50"));
        private static readonly IBrush OfflineBadgeBrush = new SolidColorBrush(Color.Parse("#8A8E9B"));
        private static readonly IBrush LobbyErrorBrush = new SolidColorBrush(Color.Parse("#FF8A8A"));
        private readonly LobbyViewModel lobbyViewModel;
        private readonly LobbySettingsViewModel settingsViewModel = new LobbySettingsViewModel();
        private readonly PlayerReportViewModel reportViewModel = new PlayerReportViewModel();
        private readonly HashSet<int> reportedUserIds = new HashSet<int>();
        private readonly List<LobbyPlayer> currentLobbyPlayers = new List<LobbyPlayer>();
        private int currentLobbyId;
        private LobbyStatus currentLobbyStatus;
        private int pendingReportUserId;
        private bool hasLeftLobby;
        private bool hasEnteredMatch;
        private bool hasReportNotice;

        public LobbyWindow()
        {
            InitializeComponent();
            DataContext = settingsViewModel;
            lobbyViewModel = new LobbyViewModel();
        }

        public LobbyWindow(LobbyDetails initialDetails, LobbyViewModel currentLobbyViewModel) : this()
        {
            lobbyViewModel = currentLobbyViewModel;
            ApplyLobbyDetails(initialDetails);
            lobbyViewModel.SubscribeToEvents(this);
            lobbyViewModel.SubscribeChat(this);
        }

        public void LobbyUpdated(LobbyDetails lobbyDetails)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!hasEnteredMatch && lobbyDetails != null)
                {
                    ApplyLobbyDetails(lobbyDetails);
                }
            });
        }

        public void MatchStarting(int startedMatchId)
        {
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
            currentLobbyId = lobbyDetails.LobbyId;
            currentLobbyStatus = lobbyDetails.Status;
            RoomCodeTextBlock.Text = string.Format(Strings.LobbyRoomIdFormat, lobbyDetails.LobbyCode);
            settingsViewModel.ApplySnapshot(lobbyDetails);

            currentLobbyPlayers.Clear();
            currentLobbyPlayers.AddRange(lobbyDetails.Players);

            if (!IsReportTargetAvailable(pendingReportUserId))
            {
                CloseReportConfirmation();
            }

            RenderPlayers(lobbyDetails.Players);
            RenderFriends();
            UpdatePlayButtonState(lobbyDetails);
        }

        private void UpdatePlayButtonState(LobbyDetails lobbyDetails)
        {
            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            bool isCurrentUserHost = lobbyDetails.HostUserId == currentSessionUser.IdUser;

            LobbyPlayButton.IsVisible = isCurrentUserHost;
            LobbyPlayButton.IsEnabled = isCurrentUserHost && lobbyDetails.CanStartMatch;
            bool isWaitingForReturns = lobbyDetails.Status == LobbyStatus.Waiting && !lobbyDetails.CanStartMatch;
            if (isWaitingForReturns && lobbyDetails.Players.Count >= MinimumPlayersToStart)
            {
                ShowLobbyMessage(Strings.LobbyWaitingForReturns);
            }
            else if (!hasReportNotice)
            {
                LobbyMessageTextBlock.IsVisible = false;
            }
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
            hasReportNotice = false;
            LobbyMessageTextBlock.Foreground = LobbyErrorBrush;
            LobbyMessageTextBlock.Text = messageText;
            LobbyMessageTextBlock.IsVisible = true;
        }

        private void OpenMatchWindow(int startedMatchId)
        {
            if (!hasEnteredMatch)
            {
                hasEnteredMatch = true;
                CloseReportConfirmation();
                hasLeftLobby = true;
                lobbyViewModel.StopSubscription();
                lobbyViewModel.StopChatSubscription();

                if (Application.Current != null &&
                    Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                {
                    MatchWindow matchWindow = new MatchWindow(startedMatchId);
                    desktopLifetime.MainWindow = matchWindow;
                    matchWindow.Show();
                    Close();
                }
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

            if (!string.IsNullOrWhiteSpace(messageText))
            {
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

        private Border CreatePlayerCard(LobbyPlayer lobbyPlayer)
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
                ColumnDefinitions = new ColumnDefinitions("*, Auto, Auto")
            };

            TextBlock nameBlock = new TextBlock
            {
                Text = displayName,
                Foreground = PlayerNameBrush,
                FontSize = PlayerNameFontSize,
                FontWeight = FontWeight.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
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
            cardGrid.Children.Add(CreateReportButton(lobbyPlayer));

            return new Border
            {
                Background = PlayerCardBackground,
                CornerRadius = new CornerRadius(PlayerCardCornerRadius),
                Padding = new Thickness(CardHorizontalPadding, CardVerticalPadding),
                Child = cardGrid
            };
        }

        private Button CreateReportButton(LobbyPlayer lobbyPlayer)
        {
            int targetUserId = InvalidUserIdentifier;
            if (lobbyPlayer.Player != null)
            {
                targetUserId = lobbyPlayer.Player.UserId;
            }
            Button reportButton = new Button
            {
                Content = Strings.PlayerReportButton, Tag = targetUserId,
                FontSize = PlayerRoleFontSize, Foreground = PlayerNameBrush,
                Margin = new Thickness(ReportButtonLeftMargin, NoMargin, NoMargin, NoMargin),
                IsVisible = PlayerReportViewModel.CanReport(lobbyPlayer, currentLobbyStatus) && !hasEnteredMatch,
                IsEnabled = !reportedUserIds.Contains(targetUserId)
            };
            reportButton.Click += ReportPlayerClick;
            Grid.SetColumn(reportButton, ReportColumnIndex);
            return reportButton;
        }

        private bool IsReportTargetAvailable(int targetUserId)
        {
            bool available = false;
            foreach (LobbyPlayer player in currentLobbyPlayers)
            {
                if (player.Player != null && player.Player.UserId == targetUserId)
                {
                    available = PlayerReportViewModel.CanReport(player, currentLobbyStatus) && !hasEnteredMatch;
                }
            }
            return available;
        }

        private void ReportPlayerClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (sender is Button reportButton && reportButton.Tag is int targetUserId)
            {
                OpenReportConfirmation(targetUserId);
            }
        }

        private void OpenReportConfirmation(int targetUserId)
        {
            bool alreadyReported = reportedUserIds.Contains(targetUserId);
            if (!alreadyReported && IsReportTargetAvailable(targetUserId))
            {
                foreach (LobbyPlayer player in currentLobbyPlayers)
                {
                    if (player.Player != null && player.Player.UserId == targetUserId)
                    {
                        pendingReportUserId = targetUserId;
                        ReportConfirmationTextBlock.Text = string.Format(Strings.PlayerReportConfirmationFormat,
                            player.Player.Nickname);
                        ReportConfirmationOverlay.IsVisible = true;
                        SetLobbyContentEnabled(false);
                        SendChatMessageButton.IsDefault = false;
                        ConfirmPlayerReportButton.IsDefault = true;
                        ConfirmPlayerReportButton.Focus();
                    }
                }
            }
        }

        private void ConfirmPlayerReportClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (IsReportTargetAvailable(pendingReportUserId))
            {
                ConfirmPlayerReportButton.IsEnabled = false;
                try
                {
                    bool duplicate = reportViewModel.ReportPlayer(currentLobbyId, pendingReportUserId);
                    reportedUserIds.Add(pendingReportUserId);
                    string message = Strings.PlayerReportSuccess;
                    if (duplicate)
                    {
                        message = Strings.PlayerReportDuplicate;
                    }
                    ShowLobbyMessage(message);
                    LobbyMessageTextBlock.Foreground = OnlineBadgeBrush;
                    hasReportNotice = true;
                    RenderPlayers(currentLobbyPlayers);
                }
                catch (DataOperationException dataOperationException)
                {
                    JsonLogger.Error(dataOperationException, "Could not report player {0} from the lobby.",
                        pendingReportUserId);
                    ShowLobbyMessage(dataOperationException.Message);
                }
                finally
                {
                    CloseReportConfirmation();
                    ConfirmPlayerReportButton.IsEnabled = true;
                }
            }
        }

        private void CancelPlayerReportClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            CloseReportConfirmation();
        }

        private void ReportConfirmationKeyDown(object? sender, KeyEventArgs keyEventArgs)
        {
            if (keyEventArgs.Key == Key.Escape)
            {
                CloseReportConfirmation();
                keyEventArgs.Handled = true;
            }
        }

        private void CloseReportConfirmation()
        {
            bool wasVisible = ReportConfirmationOverlay.IsVisible;
            pendingReportUserId = InvalidUserIdentifier;
            ReportConfirmationOverlay.IsVisible = false;
            SetLobbyContentEnabled(true);
            ConfirmPlayerReportButton.IsDefault = false;
            SendChatMessageButton.IsDefault = true;
            if (wasVisible && !hasEnteredMatch)
            {
                ChatInputTextBox.Focus();
            }
        }

        private void SetLobbyContentEnabled(bool isEnabled)
        {
            foreach (Control lobbyControl in LobbyLayoutGrid.Children)
            {
                if (!ReferenceEquals(lobbyControl, ReportConfirmationOverlay))
                {
                    lobbyControl.IsEnabled = isEnabled;
                }
            }
        }

        private void RenderFriends()
        {
            FriendsStackPanel.Children.Clear();

            SessionUser currentSessionUser = SessionService.GetOrCreateCurrentUser();
            if (currentSessionUser.IsGuest)
            {
                FriendsStackPanel.Children.Add(CreateFriendsNotice(Strings.LobbyFriendsGuestDisabled));
            }
            else
            {
                PopulateRegisteredFriends();
            }
        }

        private void PopulateRegisteredFriends()
        {
            try
            {
                List<FriendSummary> friends = lobbyViewModel.GetFriends();
                if (friends.Count == 0)
                {
                    FriendsStackPanel.Children.Add(CreateFriendsNotice(Strings.LobbyNoFriendsMessage));
                }
                else
                {
                    foreach (FriendSummary friend in friends)
                    {
                        Border friendCard = CreateFriendCard(friend);
                        FriendsStackPanel.Children.Add(friendCard);
                    }
                }
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not populate friends in lobby window.");
            }
        }

        private static TextBlock CreateFriendsNotice(string noticeText)
        {
            return new TextBlock
            {
                Text = noticeText,
                Foreground = OtherChatMessageBrush,
                FontSize = FriendsNoticeFontSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, FriendsNoticeTopMargin, 0, 0)
            };
        }

        private Border CreateFriendCard(FriendSummary friendSummary)
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
                ColumnDefinitions = new ColumnDefinitions("*, Auto, Auto")
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

            Button invitationButton = new Button
            {
                Content = Strings.LobbyInviteButton,
                Tag = friendSummary.UserId,
                Foreground = statusBrush,
                FontSize = PlayerRoleFontSize,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            invitationButton.Click += InviteFriendClick;
            Grid.SetColumn(invitationButton, InvitationColumnIndex);

            cardGrid.Children.Add(nameBlock);
            cardGrid.Children.Add(statusBlock);
            cardGrid.Children.Add(invitationButton);

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

        private void InviteFriendClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (sender is Button invitationButton && invitationButton.Tag is int friendUserId)
            {
                try
                {                   
                    lobbyViewModel.InviteFriend(friendUserId);
                    invitationButton.Content = Strings.LobbyInvitedButton;
                    invitationButton.IsEnabled = false;
                }
                catch (DataOperationException dataOperationException)
                {
                    JsonLogger.Error(dataOperationException, "Could not send lobby invitation to friend {0}.",
                        friendUserId);
                    ShowLobbyMessage(dataOperationException.Message);
                }
            }
        }
    }
}
