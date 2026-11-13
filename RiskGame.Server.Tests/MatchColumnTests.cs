using System.Collections.Generic;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class MatchColumnTests
    {
        private const int CentralColumnNumber = 7;
        private const int CentralColumnHeight = 13;
        private const int SampleUserId = 15;
        private const int ExpectedInitialStep = 0;

        [Fact]
        public void Constructor_ValidColumnNumber_StoresColumnNumber()
        {
            List<MatchParticipant> participants = new List<MatchParticipant>();

            MatchColumn column = new MatchColumn(CentralColumnNumber, CentralColumnHeight, participants);

            Assert.Equal(CentralColumnNumber, column.ColumnNumber);
        }

        [Fact]
        public void Constructor_ValidHeight_StoresMaximumSteps()
        {
            List<MatchParticipant> participants = new List<MatchParticipant>();

            MatchColumn column = new MatchColumn(CentralColumnNumber, CentralColumnHeight, participants);

            Assert.Equal(CentralColumnHeight, column.MaximumSteps);
        }

        [Fact]
        public void Constructor_WithParticipant_InitializesStepsAtZero()
        {
            LobbyPlayer lobbyPlayer = new LobbyPlayer { Player = new PlayerSummary { UserId = SampleUserId } };
            List<MatchParticipant> participants = new List<MatchParticipant> { new MatchParticipant(lobbyPlayer) };

            MatchColumn column = new MatchColumn(CentralColumnNumber, CentralColumnHeight, participants);

            Assert.Equal(ExpectedInitialStep, column.StepsByUserId[SampleUserId]);
        }
    }
}
