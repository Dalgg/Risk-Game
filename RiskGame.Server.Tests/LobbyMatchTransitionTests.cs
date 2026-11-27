using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class LobbyMatchTransitionTests
    {
        [Fact]
        public void StartMatch_KeepsLobbyStreamRegisteredUntilClientCancels()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();
            LobbySettingsBuilder.SettingsStream stream = new LobbySettingsBuilder.SettingsStream();
            setup.Manager.RegisterSubscriber(LobbySettingsBuilder.HostId, stream);

            setup.Manager.StartMatch(LobbySettingsBuilder.HostId);

            Assert.True(setup.Manager.IsLobbySubscriberCurrent(LobbySettingsBuilder.HostId, stream));
        }

        [Fact]
        public void StartMatch_DeliversMatchIdentifierThroughLobbyStream()
        {
            LobbySettingsBuilder setup = new LobbySettingsBuilder();
            LobbySettingsBuilder.SettingsStream stream = new LobbySettingsBuilder.SettingsStream();
            setup.Manager.RegisterSubscriber(LobbySettingsBuilder.HostId, stream);

            int startedMatchId = setup.Manager.StartMatch(LobbySettingsBuilder.HostId);

            Assert.Equal(startedMatchId, stream.Read().StartedMatchId);
        }
    }
}
