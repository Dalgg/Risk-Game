extern alias Client;

using Avalonia.Controls;
using RiskGame.Server.Tests.Builders;
using Xunit;
using BoardPlayer = Client::RiskGame.Contracts.Match.BoardPlayer;
using BoardState = Client::RiskGame.Contracts.Match.BoardState;
using ClientStrings = Client::RiskGame.Resources.Strings;
using MatchEndReason = Client::RiskGame.Contracts.Match.MatchEndReason;
using MatchResultWindow = Client::RiskGame.Views.MatchResultWindow;
using SessionService = Client::RiskGame.Services.SessionService;
using SessionUser = Client::RiskGame.Services.SessionUser;

namespace RiskGame.Server.Tests
{
    public sealed class MatchResultWindowTests
    {
        private const int MatchId = 77;
        private const int WinnerId = 41;
        private const int AbandonedId = 42;
        private const int GuestId = -41;
        private const string WinnerName = "winner.player";
        private const string AbandonedName = "idle.player";
        private const string ReturnButtonName = "ReturnButton";
        private const string MenuButtonName = "MenuButton";
        private const string ErrorTextBlockName = "ErrorTextBlock";

        [Fact]
        public void MatchResultWindow_AbandonedCurrentPlayer_HidesReturnAndShowsMenuButton()
        {
            ProfileTestApplication.Run(() =>
            {
                SessionUser? previousSession = SessionService.CurrentUser;
                try
                {
                    SessionService.Login(new SessionUser(AbandonedId, AbandonedName, AbandonedName));
                    BoardState state = CreateAbandonmentState();
                    MatchResultWindow window = new MatchResultWindow(state);

                    Button returnButton = window.FindControl<Button>(ReturnButtonName)!;
                    Button menuButton = window.FindControl<Button>(MenuButtonName)!;
                    TextBlock errorBlock = window.FindControl<TextBlock>(ErrorTextBlockName)!;

                    Assert.False(returnButton.IsVisible);
                    Assert.False(returnButton.IsEnabled);
                    Assert.True(menuButton.IsVisible);
                    Assert.Equal(ClientStrings.MatchResultAbandonedNotice, errorBlock.Text);
                }
                finally
                {
                    RestoreSession(previousSession);
                }
            });
        }

        [Fact]
        public void MatchResultWindow_ActiveWinner_ShowsReturnButtonAndHidesMenuButton()
        {
            ProfileTestApplication.Run(() =>
            {
                SessionUser? previousSession = SessionService.CurrentUser;
                try
                {
                    SessionService.Login(new SessionUser(WinnerId, WinnerName, WinnerName));
                    BoardState state = CreateAbandonmentState();
                    MatchResultWindow window = new MatchResultWindow(state);

                    Button returnButton = window.FindControl<Button>(ReturnButtonName)!;
                    Button menuButton = window.FindControl<Button>(MenuButtonName)!;

                    Assert.True(returnButton.IsVisible);
                    Assert.True(returnButton.IsEnabled);
                    Assert.False(menuButton.IsVisible);
                }
                finally
                {
                    RestoreSession(previousSession);
                }
            });
        }

        private static BoardState CreateAbandonmentState()
        {
            BoardState state = new BoardState
            {
                MatchId = MatchId,
                IsFinished = true,
                WinnerUserId = WinnerId,
                EndReason = MatchEndReason.Abandonment
            };
            state.Players.Add(new BoardPlayer { UserId = WinnerId, Nickname = WinnerName, IsConnected = true });
            state.Players.Add(new BoardPlayer
            {
                UserId = AbandonedId, Nickname = AbandonedName, IsConnected = false, HasAbandoned = true
            });
            return state;
        }

        private static void RestoreSession(SessionUser? previousSession)
        {
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
