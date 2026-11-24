using System;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class LobbySettingsTests
    {
        private const int ShortSeconds = 30;
        private const int DefaultSeconds = 60;
        private const int LongSeconds = 90;
        private const int InvalidSeconds = 45;
        private const int InvalidVariant = 99;

        [Theory]
        [InlineData(RulesVariant.Standard, ShortSeconds)]
        [InlineData(RulesVariant.Standard, DefaultSeconds)]
        [InlineData(RulesVariant.Standard, LongSeconds)]
        [InlineData(RulesVariant.Fast, ShortSeconds)]
        [InlineData(RulesVariant.Fast, DefaultSeconds)]
        [InlineData(RulesVariant.Fast, LongSeconds)]
        public void Validate_SupportedRules_Accepts(RulesVariant variant, int seconds)
        {
            LobbyRules rules = new LobbyRules { RulesVariant = variant, TurnDurationSeconds = seconds };

            Exception? exception = Record.Exception(() => LobbyRulesValidator.Validate(rules));

            Assert.Null(exception);
        }

        [Fact]
        public void Validate_UnsupportedDuration_Rejects()
        {
            LobbyRules rules = new LobbyRules { TurnDurationSeconds = InvalidSeconds };

            Assert.Throws<ArgumentException>(() => LobbyRulesValidator.Validate(rules));
        }

        [Fact]
        public void Validate_UnsupportedVariant_Rejects()
        {
            LobbyRules rules = new LobbyRules
            {
                RulesVariant = (RulesVariant)InvalidVariant, TurnDurationSeconds = DefaultSeconds
            };

            Assert.Throws<ArgumentException>(() => LobbyRulesValidator.Validate(rules));
        }

        [Fact]
        public void UpdateRules_Host_ChangesSnapshot()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();

            LobbyDetails details = setup.UpdateHostRules();

            Assert.Equal(RulesVariant.Fast, details.Rules.RulesVariant);
        }

        [Fact]
        public void UpdateRules_Host_PersistsDuration()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();

            setup.UpdateHostRules();

            Assert.Equal(LongSeconds, setup.Repository.SavedDuration);
        }

        [Fact]
        public void UpdateRules_Host_PersistsVariant()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();

            setup.UpdateHostRules();

            Assert.Equal("Fast", setup.Repository.SavedVariant);
        }

        [Fact]
        public void UpdateRules_Host_NotifiesOtherPlayer()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();
            LobbySettingsBuilder.SettingsStream stream = new LobbySettingsBuilder.SettingsStream();
            setup.Manager.RegisterSubscriber(LobbySettingsBuilder.OtherId, stream);

            setup.UpdateHostRules();

            Assert.Equal(LongSeconds, stream.Read().Lobby.Rules.TurnDurationSeconds);
        }

        [Fact]
        public void UpdateRules_NonHost_Rejects()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();

            Assert.Throws<InvalidOperationException>(() =>
                setup.Manager.UpdateRules(LobbySettingsBuilder.OtherId, setup.Token, setup.UpdatedRules()));
        }

        [Fact]
        public void UpdateRules_ExpiredSession_Rejects()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();
            setup.ExpireSession();

            Assert.Throws<InvalidSessionException>(() => setup.UpdateHostRules());
        }

        [Fact]
        public void UpdateRules_StartedMatch_Rejects()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();
            setup.Manager.StartMatch(LobbySettingsBuilder.HostId);

            Assert.Throws<InvalidOperationException>(() => setup.UpdateHostRules());
        }

        [Fact]
        public void UpdateRules_SaveFailure_KeepsPreviousRules()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();
            setup.Repository.FailSave = true;

            Record.Exception(() => setup.UpdateHostRules());

            Assert.Equal(DefaultSeconds, setup.Snapshot().Rules.TurnDurationSeconds);
        }

        [Fact]
        public void UpdateRules_SaveFailure_Propagates()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();
            setup.Repository.FailSave = true;

            Assert.Throws<DataOperationException>(() => setup.UpdateHostRules());
        }

        [Fact]
        public void UpdateRules_UnsupportedDuration_DoesNotPersist()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();
            LobbyRules rules = setup.UpdatedRules();
            rules.TurnDurationSeconds = InvalidSeconds;

            Assert.Throws<ArgumentException>(() =>
                setup.Manager.UpdateRules(LobbySettingsBuilder.HostId, setup.Token, rules));
        }

        [Fact]
        public void UpdateRules_LaterChange_DoesNotChangeExistingMatch()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();
            ActiveMatch match = new ActiveMatch(LobbySettingsBuilder.HostId, setup.Snapshot());

            setup.UpdateHostRules();

            Assert.Equal(RulesVariant.Standard, match.RulesVariant);
        }

        [Fact]
        public void UpdateRules_LaterChange_DoesNotChangeExistingMatchDuration()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();
            ActiveMatch match = new ActiveMatch(LobbySettingsBuilder.HostId, setup.Snapshot());

            setup.UpdateHostRules();

            Assert.Equal(DefaultSeconds, match.TurnDurationSeconds);
        }
    }
}
