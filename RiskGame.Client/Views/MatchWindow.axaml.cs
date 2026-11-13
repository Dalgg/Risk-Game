using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using RiskGame.Callbacks;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Match;
using RiskGame.Resources;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class MatchWindow : Window, IMatchCallback
    {
        private const int MinimumValidMatchId = 1;
        private const int InitialStep = 0;
        private const int FirstBoardStep = 1;
        private const int CellWidth = 42;
        private const int CellHeight = 22;
        private const int MarkerSize = 8;
        private const int MarkerSpacing = 2;
        private const int ColumnSpacing = 8;
        private const int CellSpacing = 3;
        private const int ColumnFontSize = 18;
        private const int PlayerFontSize = 16;
        private const int StatusFontSize = 12;
        private const int CardPadding = 10;
        private const int CardCornerRadius = 6;
        private const int CellCornerRadius = 4;
        private const int CellBorderThickness = 1;
        private const int PlayerIndicatorSize = 16;
        private static readonly IBrush CellBackground = new SolidColorBrush(Color.Parse("#25293A"));
        private static readonly IBrush CellBorderBrush = new SolidColorBrush(Color.Parse("#42485B"));
        private static readonly IBrush ActiveTurnBrush = new SolidColorBrush(Color.Parse("#D4A32D"));
        private static readonly IBrush RedPlayerBrush = new SolidColorBrush(Color.Parse("#E5484D"));
        private static readonly IBrush BluePlayerBrush = new SolidColorBrush(Color.Parse("#3E63DD"));
        private static readonly IBrush GreenPlayerBrush = new SolidColorBrush(Color.Parse("#30A46C"));
        private static readonly IBrush YellowPlayerBrush = new SolidColorBrush(Color.Parse("#F5D90A"));
        private readonly MatchViewModel matchViewModel = new MatchViewModel();
        private int matchIdentifier;
        private long latestRevision;
        private bool hasClosed;

        public MatchWindow()
        {
            InitializeComponent();
        }

        public MatchWindow(int matchId) : this()
        {
            matchIdentifier = matchId;
        }

        public void MatchUpdated(BoardState boardState)
        {
            Dispatcher.UIThread.Post(() => ApplyBoardState(boardState));
        }

        public void MatchStreamStopped(string statusMessage)
        {
            Dispatcher.UIThread.Post(() => ShowStreamStatus(statusMessage));
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            hasClosed = true;
            matchViewModel.StopSubscription();
            base.OnClosed(eventArgs);
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs routedEventArgs)
        {
            ReconnectButton.Focus();
            StartSubscription();
        }

        private void ReconnectClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            StartSubscription();
        }

        private void StartSubscription()
        {
            if (matchIdentifier >= MinimumValidMatchId)
            {
                MatchStatusTextBlock.Text = Strings.MatchConnecting;
                matchViewModel.Subscribe(matchIdentifier, this);
            }
        }

        private void ApplyBoardState(BoardState boardState)
        {
            if (!hasClosed && boardState.MatchId == matchIdentifier && boardState.Revision >= latestRevision)
            {
                latestRevision = boardState.Revision;
                MatchIdTextBlock.Text = string.Format(Strings.MatchIdFormat, boardState.MatchId);
                RenderBoard(boardState);
                RenderPlayers(boardState);
                RenderTurn(boardState);
                MatchStatusTextBlock.Text = Strings.MatchSynchronized;
            }
        }

        private void RenderTurn(BoardState boardState)
        {
            string currentPlayerName = Strings.LobbyDefaultPlayerName;
            foreach (BoardPlayer player in boardState.Players)
            {
                if (player.UserId == boardState.CurrentTurnUserId)
                {
                    currentPlayerName = player.Nickname;
                }
            }
            CurrentTurnTextBlock.Text = string.Format(Strings.MatchCurrentTurnFormat, currentPlayerName);
            string rulesName = Strings.MatchStandardRules;
            if (boardState.RulesVariant == RulesVariant.Fast)
            {
                rulesName = Strings.MatchFastRules;
            }
            MatchRulesTextBlock.Text = string.Format(Strings.MatchRulesFormat,
                rulesName, boardState.TurnDurationSeconds);
        }

        private void RenderBoard(BoardState boardState)
        {
            MatchBoardStackPanel.Children.Clear();
            foreach (BoardColumn column in boardState.Columns)
            {
                StackPanel columnPanel = new StackPanel
                {
                    Spacing = CellSpacing, VerticalAlignment = VerticalAlignment.Bottom
                };
                for (int step = column.MaximumSteps; step >= FirstBoardStep; step--)
                {
                    Border cell = new Border
                    {
                        Width = CellWidth, Height = CellHeight, Background = CellBackground,
                        BorderBrush = CellBorderBrush, BorderThickness = new Thickness(CellBorderThickness),
                        CornerRadius = new CornerRadius(CellCornerRadius),
                        Child = CreateMarkers(column, step, boardState)
                    };
                    ToolTip.SetTip(cell, string.Format(Strings.MatchCellFormat, column.ColumnNumber, step));
                    columnPanel.Children.Add(cell);
                }
                Border startingArea = new Border
                {
                    Width = CellWidth, Height = CellHeight,
                    Child = CreateMarkers(column, InitialStep, boardState)
                };
                columnPanel.Children.Add(startingArea);
                columnPanel.Children.Add(new TextBlock
                {
                    Text = column.ColumnNumber.ToString(CultureInfo.CurrentCulture),
                    Foreground = Brushes.White, FontSize = ColumnFontSize,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                MatchBoardStackPanel.Children.Add(columnPanel);
            }
        }

        private StackPanel CreateMarkers(BoardColumn column, int step, BoardState boardState)
        {
            StackPanel markers = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = MarkerSpacing,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            foreach (BoardPlayer player in boardState.Players)
            {
                if (column.StepsByUserId.ContainsKey(player.UserId) && column.StepsByUserId[player.UserId] == step)
                {
                    markers.Children.Add(new Border
                    {
                        Width = MarkerSize, Height = MarkerSize, CornerRadius = new CornerRadius(MarkerSize),
                        Background = ResolvePlayerBrush(player.Color)
                    });
                }
            }
            return markers;
        }

        private void RenderPlayers(BoardState boardState)
        {
            MatchPlayersStackPanel.Children.Clear();
            foreach (BoardPlayer player in boardState.Players)
            {
                StackPanel playerDetails = new StackPanel { Spacing = CellSpacing };
                playerDetails.Children.Add(new TextBlock
                {
                    Text = player.Nickname, Foreground = Brushes.White, FontSize = PlayerFontSize,
                    TextWrapping = TextWrapping.Wrap
                });
                string connectionStatus = Strings.MatchParticipantDisconnected;
                if (player.IsConnected)
                {
                    connectionStatus = Strings.MatchParticipantConnected;
                }
                playerDetails.Children.Add(new TextBlock
                {
                    Text = connectionStatus, Foreground = Brushes.White, FontSize = StatusFontSize
                });
                IBrush borderBrush = CellBorderBrush;
                if (player.UserId == boardState.CurrentTurnUserId)
                {
                    borderBrush = ActiveTurnBrush;
                }
                StackPanel cardContent = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = ColumnSpacing
                };
                cardContent.Children.Add(new Border
                {
                    Width = PlayerIndicatorSize, Height = PlayerIndicatorSize,
                    Background = ResolvePlayerBrush(player.Color),
                    CornerRadius = new CornerRadius(PlayerIndicatorSize), VerticalAlignment = VerticalAlignment.Center
                });
                cardContent.Children.Add(playerDetails);
                MatchPlayersStackPanel.Children.Add(new Border
                {
                    Child = cardContent, Background = CellBackground, BorderBrush = borderBrush,
                    BorderThickness = new Thickness(CellBorderThickness), Padding = new Thickness(CardPadding),
                    CornerRadius = new CornerRadius(CardCornerRadius)
                });
            }
        }

        private static IBrush ResolvePlayerBrush(PlayerColor playerColor)
        {
            switch (playerColor)
            {
                case PlayerColor.Red:
                    return RedPlayerBrush;
                case PlayerColor.Blue:
                    return BluePlayerBrush;
                case PlayerColor.Green:
                    return GreenPlayerBrush;
                case PlayerColor.Yellow:
                    return YellowPlayerBrush;
                default:
                    throw new ArgumentOutOfRangeException(nameof(playerColor));
            }
        }

        private void ShowStreamStatus(string statusMessage)
        {
            if (!hasClosed)
            {
                MatchStatusTextBlock.Text = statusMessage;
            }
        }

        private void LeaveMatchClick(object? sender, RoutedEventArgs routedEventArgs)
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
    }
}
