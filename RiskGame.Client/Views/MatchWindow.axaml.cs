using System;
using System.Globalization;
using System.Threading.Tasks;
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
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;
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
        private const int FirstPairIndex = 1;
        private const int SecondPairIndex = 2;
        private const int ThirdPairIndex = 3;
        private const int NoDice = 0;
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
        private bool hasLiveStream;
        private bool isActionPending;
        private BoardState? currentBoardState;

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
            Dispatcher.UIThread.Post(() => ReceiveStreamState(boardState));
        }

        private void ReceiveStreamState(BoardState boardState)
        {
            hasLiveStream = true;
            ApplyBoardState(boardState);
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
                hasLiveStream = false;
                UpdateActionButtons();
                MatchStatusTextBlock.Text = Strings.MatchConnecting;
                matchViewModel.Subscribe(matchIdentifier, this);
            }
        }

        private void ApplyBoardState(BoardState boardState)
        {
            if (!hasClosed && boardState.MatchId == matchIdentifier && boardState.Revision >= latestRevision)
            {
                latestRevision = boardState.Revision;
                currentBoardState = boardState;
                MatchIdTextBlock.Text = string.Format(Strings.MatchIdFormat, boardState.MatchId);
                RenderBoard(boardState);
                RenderPlayers(boardState);
                RenderTurn(boardState);
                RenderDice(boardState);
                MatchStatusTextBlock.Text = Strings.MatchSynchronized;
            }
            UpdateActionButtons();
        }

        private void RenderDice(BoardState boardState)
        {
            DiceValuesTextBlock.Text = Strings.MatchNoRoll;
            PairSelectionTextBlock.Text = Strings.MatchRollPrompt;
            FirstPairButton.IsVisible = false;
            SecondPairButton.IsVisible = false;
            ThirdPairButton.IsVisible = false;
            if (boardState.Dice.Count > NoDice)
            {
                DiceValuesTextBlock.Text = string.Format(Strings.MatchDiceFormat,
                    string.Join(Strings.MatchDiceSeparator, boardState.Dice));
                PairSelectionTextBlock.Text = Strings.MatchChoosePairPrompt;
                if (!boardState.AwaitingPairChoice)
                {
                    PairSelectionTextBlock.Text = string.Format(Strings.MatchSelectedPairFormat,
                        boardState.SelectedPairIndex);
                }
            }
            foreach (PairOption pairOption in boardState.PairOptions)
            {
                Button pairButton = GetPairButton(pairOption.PairIndex);
                pairButton.IsVisible = true;
                pairButton.Content = string.Format(Strings.MatchPairOptionFormat, new object[]
                {
                    pairOption.PairIndex,
                    string.Join(Strings.MatchPairSeparator, pairOption.FirstPairDice), pairOption.FirstSum,
                    string.Join(Strings.MatchPairSeparator, pairOption.SecondPairDice), pairOption.SecondSum
                });
                pairButton.Background = CellBackground;
                if (pairOption.PairIndex == boardState.SelectedPairIndex)
                {
                    pairButton.Background = ActiveTurnBrush;
                }
            }
        }

        private Button GetPairButton(int pairIndex)
        {
            switch (pairIndex)
            {
                case FirstPairIndex:
                    return FirstPairButton;
                case SecondPairIndex:
                    return SecondPairButton;
                case ThirdPairIndex:
                    return ThirdPairButton;
                default:
                    throw new ArgumentOutOfRangeException(nameof(pairIndex));
            }
        }

        private void UpdateActionButtons()
        {
            RollDiceButton.IsEnabled = false;
            FirstPairButton.IsEnabled = false;
            SecondPairButton.IsEnabled = false;
            ThirdPairButton.IsEnabled = false;
            if (currentBoardState != null && hasLiveStream && !hasClosed && !isActionPending
                && currentBoardState.CurrentTurnUserId == SessionService.GetOrCreateCurrentUser().IdUser)
            {
                RollDiceButton.IsEnabled = !currentBoardState.AwaitingPairChoice;
                FirstPairButton.IsEnabled = currentBoardState.AwaitingPairChoice;
                SecondPairButton.IsEnabled = currentBoardState.AwaitingPairChoice;
                ThirdPairButton.IsEnabled = currentBoardState.AwaitingPairChoice;
            }
        }

        private void RollDiceClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (RollDiceButton.IsEnabled && currentBoardState != null)
            {
                long expectedRollId = currentBoardState.RollId;
                isActionPending = true;
                UpdateActionButtons();
                Task.Run(() => SubmitRoll(expectedRollId));
            }
        }

        private void ChoosePairClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (sender is Button pairButton && pairButton.IsEnabled && pairButton.Tag is int pairIndex
                && currentBoardState != null)
            {
                long rollId = currentBoardState.RollId;
                isActionPending = true;
                UpdateActionButtons();
                Task.Run(() => SubmitPairChoice(rollId, pairIndex));
            }
        }

        private void SubmitRoll(long expectedRollId)
        {
            try
            {
                BoardState boardState = matchViewModel.RollDice(matchIdentifier, expectedRollId);
                Dispatcher.UIThread.Post(() => CompleteAction(boardState));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not submit the dice roll.");
                Dispatcher.UIThread.Post(() => ShowActionError(dataOperationException.Message));
            }
        }

        private void SubmitPairChoice(long rollId, int pairIndex)
        {
            try
            {
                BoardState boardState = matchViewModel.ChooseDicePair(matchIdentifier, rollId, pairIndex);
                Dispatcher.UIThread.Post(() => CompleteAction(boardState));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not submit the dice pair choice.");
                Dispatcher.UIThread.Post(() => ShowActionError(dataOperationException.Message));
            }
        }

        private void CompleteAction(BoardState boardState)
        {
            isActionPending = false;
            ApplyBoardState(boardState);
        }

        private void ShowActionError(string errorMessage)
        {
            isActionPending = false;
            if (!hasClosed)
            {
                MatchStatusTextBlock.Text = errorMessage;
                UpdateActionButtons();
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
                hasLiveStream = false;
                UpdateActionButtons();
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
