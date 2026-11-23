extern alias Client;

using Avalonia.Controls;
using RiskGame.Server.Tests.Builders;
using Xunit;
using BoardState = Client::RiskGame.Contracts.Match.BoardState;
using BoardPlayer = Client::RiskGame.Contracts.Match.BoardPlayer;
using MatchEndReason = Client::RiskGame.Contracts.Match.MatchEndReason;
using MatchResultWindow = Client::RiskGame.Views.MatchResultWindow;
using Strings = Client::RiskGame.Resources.Strings;

namespace RiskGame.Server.Tests
{
    public sealed class MatchDisconnectWindowTests
    {
        private const int WinnerId = 41;
        private const string WinnerNickname = "Winner";

        [Fact]
        public void Result_Abandonment_ExplainsWinner()
        {
            ProfileTestApplication.Run(() =>
            {
                BoardState state = new BoardState
                {
                    IsFinished = true, WinnerUserId = WinnerId, EndReason = MatchEndReason.Abandonment
                };
                state.Players.Add(new BoardPlayer { UserId = WinnerId, Nickname = WinnerNickname });

                MatchResultWindow window = new MatchResultWindow(state);

                Assert.Equal(string.Format(Strings.MatchWinnerByAbandonmentFormat, WinnerNickname),
                    window.FindControl<TextBlock>("WinnerTextBlock")!.Text);
            });
        }

        [Fact]
        public void Result_Cancelled_ExplainsAbsenceOfWinner()
        {
            ProfileTestApplication.Run(() =>
            {
                BoardState state = new BoardState { IsFinished = true, EndReason = MatchEndReason.Cancelled };

                MatchResultWindow window = new MatchResultWindow(state);

                Assert.Equal(Strings.MatchCancelledNoWinner, window.FindControl<TextBlock>("WinnerTextBlock")!.Text);
            });
        }

        [Fact]
        public void Result_Cancelled_OffersMenu()
        {
            ProfileTestApplication.Run(() =>
            {
                BoardState state = new BoardState { IsFinished = true, EndReason = MatchEndReason.Cancelled };

                MatchResultWindow window = new MatchResultWindow(state);

                Assert.True(window.FindControl<Button>("MenuButton")!.IsVisible);
            });
        }
    }
}
