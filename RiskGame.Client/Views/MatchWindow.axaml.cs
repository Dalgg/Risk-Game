using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
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
        private const int UnassignedMatchId = 0;
        private const int InitialStep = 0;
        private const int FirstBoardStep = 1;
        private const int CellWidth = 48;
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
        private const int NoKeyboardOption = 0;
        private const int CountdownIntervalMilliseconds = 200;
        private static readonly IBrush CellBackground = new ImmutableSolidColorBrush(Color.Parse("#25293A"));
        private static readonly IBrush CellBorderBrush = new ImmutableSolidColorBrush(Color.Parse("#42485B"));
        private static readonly IBrush ActiveTurnBrush = new ImmutableSolidColorBrush(Color.Parse("#D4A32D"));
        private static readonly IBrush RedPlayerBrush = new ImmutableSolidColorBrush(Color.Parse("#E5484D"));
        private static readonly IBrush BluePlayerBrush = new ImmutableSolidColorBrush(Color.Parse("#3E63DD"));
        private static readonly IBrush GreenPlayerBrush = new ImmutableSolidColorBrush(Color.Parse("#30A46C"));
        private static readonly IBrush YellowPlayerBrush = new ImmutableSolidColorBrush(Color.Parse("#F5D90A"));
        private readonly MatchViewModel matchViewModel;
        private readonly DispatcherTimer countdownTimer = new DispatcherTimer();
        private int matchIdentifier;
        private long latestRevision;
        private bool hasClosed;
        private bool hasOpenedResult;
        private bool hasLiveStream;
        private bool isConnecting;
        private bool isActionPending;
        private bool isLeavingMatch;
        private bool hasLeftMatch;
        private BoardState? currentBoardState;
        private PairChoice? pendingColumnChoice;
        private int subscriptionVersion;

        public MatchWindow() : this(UnassignedMatchId, new MatchViewModel())
        {
        }

        public MatchWindow(int matchId) : this(matchId, new MatchViewModel())
        {
        }

        public MatchWindow(int matchId, MatchViewModel matchViewModel)
        {
            this.matchViewModel = matchViewModel;
            matchIdentifier = matchId;
            InitializeComponent();
            DataContext = matchViewModel;
            countdownTimer.Interval = TimeSpan.FromMilliseconds(CountdownIntervalMilliseconds);
            countdownTimer.Tick += OnCountdownTick;
        }

        public void MatchUpdated(BoardState boardState)
        {
            QueueBoardState(boardState, subscriptionVersion);
        }

        private void QueueBoardState(BoardState state, int version)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!hasClosed && version == subscriptionVersion)
                {
                    ReceiveStreamState(state);
                }
            });
        }

        private void ReceiveStreamState(BoardState boardState)
        {
            isConnecting = false;
            hasLiveStream = true;
            ApplyBoardState(boardState);
        }

        public void MatchStreamStopped(string statusMessage)
        {
            QueueStreamStatus(statusMessage, subscriptionVersion);
        }

        private void QueueStreamStatus(string message, int version)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!hasClosed && version == subscriptionVersion)
                {
                    ShowStreamStatus(message);
                }
            });
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            hasClosed = true;
            countdownTimer.Stop();
            countdownTimer.Tick -= OnCountdownTick;
            GamepadService.ActionTriggered -= OnGamepadAction;
            matchViewModel.StopSubscription();
            bool hasExitedCleanly = hasOpenedResult || hasLeftMatch;
            bool shouldNotifyLeave = !hasExitedCleanly && !isLeavingMatch;
            if (shouldNotifyLeave && matchIdentifier >= MinimumValidMatchId)
            {
                Task.Run(LeaveClosedMatch);
            }
            base.OnClosed(eventArgs);
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs routedEventArgs)
        {
            GamepadService.ActionTriggered += OnGamepadAction;
            countdownTimer.Start();
            ReconnectButton.Focus();
            StartSubscription();
        }

        private void OnCountdownTick(object? sender, EventArgs eventArgs)
        {
            matchViewModel.Countdown.Refresh();
            UpdateActionButtons();
        }

        private void OnCombinationKeyDown(object? sender, KeyEventArgs keyEventArgs)
        {
            bool canUseShortcut = !keyEventArgs.Handled && keyEventArgs.KeyModifiers == KeyModifiers.None;
            if (canUseShortcut)
            {
                int optionIndex = GetKeyboardOptionIndex(keyEventArgs.Key);
                if (optionIndex != NoKeyboardOption)
                {
                    TriggerOptionSlot(optionIndex);
                    keyEventArgs.Handled = true;
                }
            }
        }

        private static int GetKeyboardOptionIndex(Key key)
        {
            int optionIndex = NoKeyboardOption;
            switch (key)
            {
                case Key.D1:
                case Key.NumPad1:
                    optionIndex = FirstPairIndex;
                    break;
                case Key.D2:
                case Key.NumPad2:
                    optionIndex = SecondPairIndex;
                    break;
                case Key.D3:
                case Key.NumPad3:
                    optionIndex = ThirdPairIndex;
                    break;
                default:
                    break;
            }
            return optionIndex;
        }

        private void OnGamepadAction(object? sender, GamepadActionEventArgs eventArgs)
        {
            Dispatcher.UIThread.Post(() => HandleGamepadAction(eventArgs.Action));
        }

        private void HandleGamepadAction(GamepadAction action)
        {
            if (!hasClosed)
            {
                switch (action)
                {
                    case GamepadAction.RollDice:
                        RollDiceClick(RollDiceButton, new RoutedEventArgs());
                        break;
                    case GamepadAction.SelectOption1:
                        TriggerOptionSlot(FirstPairIndex);
                        break;
                    case GamepadAction.SelectOption2:
                        TriggerOptionSlot(SecondPairIndex);
                        break;
                    case GamepadAction.SelectOption3:
                        TriggerOptionSlot(ThirdPairIndex);
                        break;
                    case GamepadAction.Cancel:
                        CancelPendingChoice();
                        break;
                    case GamepadAction.NavigateUp:
                    case GamepadAction.NavigateDown:
                    case GamepadAction.NavigateLeft:
                    case GamepadAction.NavigateRight:
                    case GamepadAction.Confirm:
                    case GamepadAction.None:
                    default:
                        break;
                }
            }
        }

        private void TriggerOptionSlot(int optionIndex)
        {
            if (ColumnChoicePanel.IsVisible)
            {
                if (optionIndex == FirstPairIndex && FirstColumnChoiceButton.IsEnabled)
                {
                    ChooseColumnClick(FirstColumnChoiceButton, new RoutedEventArgs());
                }
                else if (optionIndex == SecondPairIndex && SecondColumnChoiceButton.IsEnabled)
                {
                    ChooseColumnClick(SecondColumnChoiceButton, new RoutedEventArgs());
                }
            }
            else
            {
                Button pairButton = GetPairButton(optionIndex);
                if (pairButton.IsVisible && pairButton.IsEnabled)
                {
                    ChoosePairClick(pairButton, new RoutedEventArgs());
                }
            }
        }

        private void CancelPendingChoice()
        {
            if (ColumnChoicePanel.IsVisible && currentBoardState != null)
            {
                pendingColumnChoice = null;
                RefreshColumnChoice(currentBoardState);
                UpdateActionButtons();
            }
        }

        private void ReconnectClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            StartSubscription();
        }

        private void StartSubscription()
        {
            bool canSubscribe = CanReconnect();
            if (canSubscribe && matchIdentifier >= MinimumValidMatchId)
            {
                int nextVersion = subscriptionVersion;
                nextVersion++;
                MatchStreamCallback callback = new MatchStreamCallback(this, nextVersion);
                if (matchViewModel.Subscribe(matchIdentifier, callback))
                {
                    subscriptionVersion = nextVersion;
                    isConnecting = true;
                    hasLiveStream = false;
                    MatchStatusTextBlock.Text = Strings.MatchConnecting;
                    UpdateActionButtons();
                }
            }
        }

        private bool CanReconnect()
        {
            bool isWindowOpen = !hasClosed && !isLeavingMatch;
            bool isWindowAvailable = isWindowOpen && !hasOpenedResult && !isActionPending;
            bool isNotConnecting = !isConnecting && !matchViewModel.IsConnecting;
            bool isDisconnected = isNotConnecting && !hasLiveStream;
            bool isMatchActive = currentBoardState == null || !currentBoardState.IsFinished;
            bool canReconnect = false;
            if (isWindowAvailable && isDisconnected)
            {
                canReconnect = isMatchActive;
            }
            return canReconnect;
        }

        private void ApplyBoardState(BoardState boardState)
        {
            bool isValidRevision = boardState.MatchId == matchIdentifier && boardState.Revision >= latestRevision;
            if (!hasClosed && isValidRevision)
            {
                latestRevision = boardState.Revision;
                currentBoardState = boardState;
                matchViewModel.Countdown.ApplySnapshot(boardState);
                MatchIdTextBlock.Text = string.Format(Strings.MatchIdFormat, boardState.MatchId);
                RenderBoard(boardState);
                RenderPlayers(boardState);
                RenderTurn(boardState);
                RenderDice(boardState);
                MatchStatusTextBlock.Text = Strings.MatchSynchronized;
                if (boardState.IsFinished && !isLeavingMatch)
                {
                    OpenResultWindow(boardState);
                }
            }
            UpdateActionButtons();
        }

        private void OpenResultWindow(BoardState boardState)
        {
            bool isAlreadyHandled = hasOpenedResult || hasClosed;
            IClassicDesktopStyleApplicationLifetime? desktopLifetime = ResolveDesktopLifetime();
            if (!isAlreadyHandled && desktopLifetime != null)
            {
                hasOpenedResult = true;
                MatchResultWindow result = new MatchResultWindow(boardState);
                desktopLifetime.MainWindow = result;
                result.Show();
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

        private void RenderDice(BoardState boardState)
        {
            TemporaryMarkersTextBlock.Text = string.Format(Strings.MatchTemporaryMarkersFormat,
                boardState.TemporaryStepsByColumn.Count, boardState.MaximumTemporaryMarkers);
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
                else if (!boardState.PairOptions.Any(pairOption => pairOption.IsPlayable))
                {
                    PairSelectionTextBlock.Text = Strings.MatchNoLegalAdvance;
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
            RefreshColumnChoice(boardState);
        }

        private void RefreshColumnChoice(BoardState boardState)
        {
            ColumnChoicePanel.IsVisible = false;
            if (pendingColumnChoice != null)
            {
                PairOption? selectedOption = boardState.PairOptions
                    .FirstOrDefault(option => option.PairIndex == pendingColumnChoice.PairIndex);
                bool matchesCurrentRoll = boardState.AwaitingPairChoice
                    && pendingColumnChoice.RollId == boardState.RollId
                    && pendingColumnChoice.TurnId == boardState.TurnId;
                if (matchesCurrentRoll && selectedOption != null && selectedOption.RequiresColumnChoice)
                {
                    ColumnChoicePanel.IsVisible = true;
                    FirstColumnChoiceButton.Content = string.Format(Strings.MatchAdvanceColumnFormat,
                        selectedOption.FirstSum);
                    SecondColumnChoiceButton.Content = string.Format(Strings.MatchAdvanceColumnFormat,
                        selectedOption.SecondSum);
                    FirstColumnChoiceButton.Tag = new PairChoice
                    {
                        RollId = boardState.RollId, PairIndex = selectedOption.PairIndex,
                        PreferredColumn = selectedOption.FirstSum, TurnId = boardState.TurnId
                    };
                    SecondColumnChoiceButton.Tag = new PairChoice
                    {
                        RollId = boardState.RollId, PairIndex = selectedOption.PairIndex,
                        PreferredColumn = selectedOption.SecondSum, TurnId = boardState.TurnId
                    };
                }
                else
                {
                    pendingColumnChoice = null;
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
            bool canStopTurn = false;
            bool canRollDice = false;
            bool canChooseFirstPair = false;
            bool canChooseSecondPair = false;
            bool canChooseThirdPair = false;
            bool canChooseColumn = false;

            BoardState? boardState = currentBoardState;
            if (boardState != null && CanInteractWithBoard(boardState))
            {
                canRollDice = !boardState.AwaitingPairChoice;
                canStopTurn = boardState.CanStop;
                canChooseFirstPair = IsPairOptionEnabled(boardState, FirstPairIndex);
                canChooseSecondPair = IsPairOptionEnabled(boardState, SecondPairIndex);
                canChooseThirdPair = IsPairOptionEnabled(boardState, ThirdPairIndex);
                canChooseColumn = ColumnChoicePanel.IsVisible;
            }

            StopTurnButton.IsEnabled = canStopTurn;
            ReconnectButton.IsEnabled = CanReconnect();
            RollDiceButton.IsEnabled = canRollDice;
            FirstPairButton.IsEnabled = canChooseFirstPair;
            SecondPairButton.IsEnabled = canChooseSecondPair;
            ThirdPairButton.IsEnabled = canChooseThirdPair;
            FirstColumnChoiceButton.IsEnabled = canChooseColumn;
            SecondColumnChoiceButton.IsEnabled = canChooseColumn;
        }

        private bool CanInteractWithBoard(BoardState boardState)
        {
            bool isWindowActive = !hasClosed && !isActionPending && !isLeavingMatch;
            bool hasReadyStream = hasLiveStream && isWindowActive;
            bool hasActiveTurn = !boardState.IsFinished && matchViewModel.Countdown.HasTimeRemaining;
            bool canInteract = false;
            if (hasReadyStream && hasActiveTurn)
            {
                canInteract = boardState.CurrentTurnUserId == SessionService.GetOrCreateCurrentUser().IdUser;
            }
            return canInteract;
        }

        private static bool IsPairOptionEnabled(BoardState boardState, int pairIndex)
        {
            if (!boardState.AwaitingPairChoice)
            {
                return false;
            }
            PairOption? option = boardState.PairOptions.FirstOrDefault(
                candidate => candidate.PairIndex == pairIndex);
            return option != null && option.IsPlayable;
        }

        private void StopTurnClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (StopTurnButton.IsEnabled && currentBoardState != null)
            {
                long turnId = currentBoardState.TurnId;
                isActionPending = true;
                UpdateActionButtons();
                Task.Run(() => SubmitStopTurn(turnId));
            }
        }

        private void SubmitStopTurn(long turnId)
        {
            try
            {
                BoardState boardState = matchViewModel.StopTurn(matchIdentifier, turnId);
                Dispatcher.UIThread.Post(() => CompleteAction(boardState));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not submit the stop turn action.");
                Dispatcher.UIThread.Post(() => ShowActionError(dataOperationException));
            }
        }

        private void RollDiceClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (RollDiceButton.IsEnabled && currentBoardState != null)
            {
                long expectedRollId = currentBoardState.RollId;
                long turnId = currentBoardState.TurnId;
                isActionPending = true;
                UpdateActionButtons();
                Task.Run(() => SubmitRoll(expectedRollId, turnId));
            }
        }

        private void ChoosePairClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (sender is Button pairButton && pairButton.IsEnabled)
            {
                TryProcessPairSelection(pairButton);
            }
        }

        private void TryProcessPairSelection(Button pairButton)
        {
            if (pairButton.Tag is int pairIndex && currentBoardState != null)
            {
                PairOption selectedOption = currentBoardState.PairOptions
                    .First(option => option.PairIndex == pairIndex);
                PairChoice choice = new PairChoice
                {
                    RollId = currentBoardState.RollId,
                    PairIndex = pairIndex,
                    TurnId = currentBoardState.TurnId
                };
                if (selectedOption.RequiresColumnChoice)
                {
                    pendingColumnChoice = choice;
                    RefreshColumnChoice(currentBoardState);
                }
                else
                {
                    BeginPairChoice(choice);
                }
                UpdateActionButtons();
            }
        }

        private void ChooseColumnClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (sender is Button choiceButton && choiceButton.IsEnabled && choiceButton.Tag is PairChoice choice)
            {
                BeginPairChoice(choice);
            }
        }

        private void BeginPairChoice(PairChoice choice)
        {
            isActionPending = true;
            pendingColumnChoice = null;
            ColumnChoicePanel.IsVisible = false;
            UpdateActionButtons();
            Task.Run(() => SubmitPairChoice(choice));
        }

        private void SubmitRoll(long expectedRollId, long turnId)
        {
            try
            {
                BoardState boardState = matchViewModel.RollDice(matchIdentifier, expectedRollId, turnId);
                Dispatcher.UIThread.Post(() => CompleteAction(boardState));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not submit the dice roll.");
                Dispatcher.UIThread.Post(() => ShowActionError(dataOperationException));
            }
        }

        private void SubmitPairChoice(PairChoice choice)
        {
            try
            {
                BoardState boardState = matchViewModel.ChooseDicePair(matchIdentifier, choice);
                Dispatcher.UIThread.Post(() => CompleteAction(boardState));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not submit the dice pair choice.");
                Dispatcher.UIThread.Post(() => ShowActionError(dataOperationException));
            }
        }

        private void CompleteAction(BoardState boardState)
        {
            isActionPending = false;
            ApplyBoardState(boardState);
        }

        internal void MarkLeftForShutdown()
        {
            hasLeftMatch = true;
        }

        internal void ShowActionError(DataOperationException dataOperationException)
        {
            isActionPending = false;
            if (!hasClosed)
            {
                if (dataOperationException is MatchConnectionException)
                {
                    subscriptionVersion++;
                    isConnecting = false;
                    hasLiveStream = false;
                }
                MatchStatusTextBlock.Text = dataOperationException.Message;
                UpdateActionButtons();
            }
        }

        private void RenderTurn(BoardState boardState)
        {
            TurnOutcomeTextBlock.Text = string.Empty;
            string currentPlayerName = Strings.LobbyDefaultPlayerName;
            foreach (BoardPlayer player in boardState.Players)
            {
                if (player.UserId == boardState.CurrentTurnUserId)
                {
                    currentPlayerName = player.Nickname;
                }
                if (player.UserId == boardState.LastTurnUserId)
                {
                    string outcomeFormat = Strings.MatchTurnSavedFormat;
                    if (boardState.LastTurnBusted)
                    {
                        outcomeFormat = Strings.MatchTurnBustedFormat;
                    }
                    if (boardState.LastTurnTimedOut)
                    {
                        outcomeFormat = Strings.MatchTurnTimedOutFormat;
                    }
                    TurnOutcomeTextBlock.Text = string.Format(outcomeFormat, player.Nickname);
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
            if (boardState.IsFinished)
            {
                BoardPlayer? winner = boardState.Players.FirstOrDefault(player =>
                    player.UserId == boardState.WinnerUserId);
                CurrentTurnTextBlock.Text = Strings.MatchFinished;
                if (winner != null)
                {
                    TurnOutcomeTextBlock.Text = string.Format(Strings.MatchWinnerFormat, winner.Nickname);
                }
            }
        }

        private void RenderBoard(BoardState boardState)
        {
            MatchBoardStackPanel.Children.Clear();
            foreach (BoardColumn column in boardState.Columns)
            {
                IBrush columnBrush = CellBorderBrush;
                BoardPlayer? owner = boardState.Players.FirstOrDefault(player => player.UserId == column.OwnerUserId);
                if (column.IsClaimed && owner != null)
                {
                    columnBrush = ResolvePlayerBrush(owner.Color);
                }
                StackPanel columnPanel = new StackPanel
                {
                    Spacing = CellSpacing, VerticalAlignment = VerticalAlignment.Bottom
                };
                for (int step = column.MaximumSteps; step >= FirstBoardStep; step--)
                {
                    Border cell = new Border
                    {
                        Width = CellWidth, Height = CellHeight, Background = CellBackground,
                        BorderBrush = columnBrush, BorderThickness = new Thickness(CellBorderThickness),
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
                if (column.IsClaimed && owner != null)
                {
                    columnPanel.Children.Add(new TextBlock
                    {
                        Text = Strings.MatchClaimedMark, Foreground = columnBrush,
                        HorizontalAlignment = HorizontalAlignment.Center, FontSize = StatusFontSize
                    });
                    ToolTip.SetTip(columnPanel,
                        string.Format(Strings.MatchColumnClaimedFormat, column.ColumnNumber, owner.Nickname));
                }
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
                bool canShowMarker = !column.IsClaimed || column.OwnerUserId == player.UserId;
                bool isPlayerAtStep = column.StepsByUserId.ContainsKey(player.UserId)
                    && column.StepsByUserId[player.UserId] == step;
                if (canShowMarker && isPlayerAtStep)
                {
                    markers.Children.Add(new Border
                    {
                        Width = MarkerSize, Height = MarkerSize, CornerRadius = new CornerRadius(MarkerSize),
                        Background = ResolvePlayerBrush(player.Color)
                    });
                }
            }
            if (boardState.TemporaryStepsByColumn.ContainsKey(column.ColumnNumber)
                && boardState.TemporaryStepsByColumn[column.ColumnNumber] == step)
            {
                Border temporaryMarker = new Border
                {
                    Width = MarkerSize, Height = MarkerSize, Background = Brushes.White,
                    BorderBrush = ActiveTurnBrush, BorderThickness = new Thickness(CellBorderThickness)
                };
                ToolTip.SetTip(temporaryMarker,
                    string.Format(Strings.MatchTemporaryMarkerFormat, column.ColumnNumber, step));
                markers.Children.Add(temporaryMarker);
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
                else if (player.HasAbandoned)
                {
                    connectionStatus = Strings.MatchParticipantAbandoned;
                }
                else if (!boardState.IsFinished)
                {
                    connectionStatus = string.Format(Strings.MatchParticipantReconnectingFormat,
                        player.ReconnectionSecondsRemaining);
                }
                playerDetails.Children.Add(new TextBlock
                {
                    Text = connectionStatus, Foreground = Brushes.White, FontSize = StatusFontSize
                });
                playerDetails.Children.Add(new TextBlock
                {
                    Text = string.Format(Strings.MatchClaimedCountFormat,
                        player.ClaimedColumns, boardState.ColumnsToWin),
                    Foreground = ActiveTurnBrush, FontSize = StatusFontSize
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
                isConnecting = false;
                hasLiveStream = false;
                UpdateActionButtons();
                MatchStatusTextBlock.Text = statusMessage;
                BoardState? boardState = currentBoardState;
                bool isConnectionError = statusMessage == Strings.MatchConnectionError;
                if (boardState != null && CanShowReconnectGrace(boardState, isConnectionError))
                {
                    MatchStatusTextBlock.Text = string.Format(Strings.MatchReconnectGraceFormat,
                        statusMessage, boardState.ReconnectionGraceSeconds);
                }
            }
        }

        private static bool CanShowReconnectGrace(BoardState boardState, bool isConnectionError)
        {
            return isConnectionError && !boardState.IsFinished;
        }

        private void LeaveMatchClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            bool isWindowBusy = hasClosed || isLeavingMatch;
            if (!isWindowBusy)
            {
                isLeavingMatch = true;
                UpdateActionButtons();
                MatchStatusTextBlock.Text = Strings.MatchLeaving;
                Task.Run(SubmitLeaveMatch);
            }
        }

        private void SubmitLeaveMatch()
        {
            try
            {
                matchViewModel.LeaveMatch(matchIdentifier);
                Dispatcher.UIThread.Post(CompleteLeaveMatch);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not abandon the active match.");
                Dispatcher.UIThread.Post(() => ShowLeaveError(dataOperationException));
            }
        }

        private void ShowLeaveError(DataOperationException dataOperationException)
        {
            isLeavingMatch = false;
            ShowActionError(dataOperationException);
        }

        private void CompleteLeaveMatch()
        {
            hasLeftMatch = true;
            OpenMenu();
        }

        private void LeaveClosedMatch()
        {
            try
            {
                matchViewModel.LeaveMatch(matchIdentifier);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not notify the server when closing the match.");
                matchViewModel.LeaveLobby();
            }
        }

        private void OpenMenu()
        {
            IClassicDesktopStyleApplicationLifetime? desktopLifetime = ResolveDesktopLifetime();
            if (!hasClosed && desktopLifetime != null)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
        }

        private sealed class MatchStreamCallback : IMatchCallback
        {
            private readonly MatchWindow window;
            private readonly int version;

            public MatchStreamCallback(MatchWindow window, int version)
            {
                this.window = window;
                this.version = version;
            }

            public void MatchUpdated(BoardState boardState)
            {
                window.QueueBoardState(boardState, version);
            }

            public void MatchStreamStopped(string statusMessage)
            {
                window.QueueStreamStatus(statusMessage, version);
            }
        }
    }
}
