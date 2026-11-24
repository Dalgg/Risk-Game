extern alias Client;

using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using BoardState = Client::RiskGame.Contracts.Match.BoardState;
using BoardPlayer = Client::RiskGame.Contracts.Match.BoardPlayer;
using PairOption = Client::RiskGame.Contracts.Match.PairOption;
using MatchWindow = Client::RiskGame.Views.MatchWindow;
using SessionService = Client::RiskGame.Services.SessionService;
using SessionUser = Client::RiskGame.Services.SessionUser;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class MatchKeyboardBuilder : IDisposable
    {
        public const int UserId = 41;
        public const int OtherUserId = 43;
        private const int GuestId = -41;
        private const int UnassignedMatchId = 0;
        private const int InitialRevision = 1;
        private const int FirstPair = 1;
        private const int SecondPair = 2;
        private const int ThirdPair = 3;
        private const int FirstSum = 3;
        private const int FirstOtherSum = 10;
        private const int SecondSum = 5;
        private const int SecondOtherSum = 8;
        private const int ThirdSum = 7;
        private const int ThirdOtherSum = 6;
        private const int RemainingMilliseconds = 90000;
        private const int MaximumMarkers = 3;
        private const int FirstTemporaryColumn = 2;
        private const int SecondTemporaryColumn = 12;
        private const string KeyboardUsername = "keyboard.user";
        private const string KeyboardNickname = "Keyboard";
        private const string ReconnectButtonName = "ReconnectButton";
        private const int InitialStep = 1;
        private static readonly int[] SampleDiceValues = new[] { 1, 2, 4, 6 };
        private readonly SessionUser? previousSession = SessionService.CurrentUser;

        public MatchKeyboardBuilder()
        {
            SessionService.Login(new SessionUser(UserId, KeyboardUsername, KeyboardNickname));
            Window = new MatchWindow();
            Window.Show();
            Dispatcher.UIThread.RunJobs();
            Snapshot = new BoardState
            {
                MatchId = UnassignedMatchId, Revision = InitialRevision, CurrentTurnUserId = UserId,
                TurnId = InitialRevision, RollId = InitialRevision, AwaitingPairChoice = true,
                TurnRemainingMilliseconds = RemainingMilliseconds, MaximumTemporaryMarkers = MaximumMarkers
            };
            Snapshot.Players.Add(new BoardPlayer { UserId = UserId, Nickname = KeyboardNickname, IsConnected = true });
            Snapshot.PairOptions.Add(CreateOption(FirstPair, FirstSum, FirstOtherSum));
            Snapshot.PairOptions.Add(CreateOption(SecondPair, SecondSum, SecondOtherSum));
            Snapshot.PairOptions.Add(CreateOption(ThirdPair, ThirdSum, ThirdOtherSum));
            Snapshot.Dice.AddRange(SampleDiceValues);
            Snapshot.TemporaryStepsByColumn.Add(FirstTemporaryColumn, InitialStep);
            Snapshot.TemporaryStepsByColumn.Add(SecondTemporaryColumn, InitialStep);
            ApplySnapshot();
        }

        public MatchWindow Window { get; }

        public BoardState Snapshot { get; }

        public void ApplySnapshot()
        {
            Window.MatchUpdated(Snapshot);
            Dispatcher.UIThread.RunJobs();
        }

        public void PressKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
        {
            Button source = Window.FindControl<Button>(ReconnectButtonName)!;
            source.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers
            });
        }

        private static PairOption CreateOption(int pairIndex, int firstSum, int secondSum)
        {
            return new PairOption
            {
                PairIndex = pairIndex, FirstSum = firstSum, SecondSum = secondSum,
                IsPlayable = true, RequiresColumnChoice = true
            };
        }

        public void Dispose()
        {
            Window.Close();
            if (previousSession != null)
            {
                SessionService.Login(previousSession);
            }
            else
            {
                SessionService.Login(new SessionUser(GuestId, string.Empty, string.Empty));
                SessionService.Logout();
            }
        }
    }
}
