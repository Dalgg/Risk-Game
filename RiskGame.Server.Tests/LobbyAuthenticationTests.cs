using Grpc.Core;
using RiskGame.Contracts.Lobby;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class LobbyAuthenticationTests
    {
        [Fact]
        public void Create_ValidRegisteredSession_Accepts()
        {
            LobbyAuthenticationBuilder setup = new LobbyAuthenticationBuilder();

            LobbyDetails lobby = setup.CreateRegisteredLobby();

            Assert.Equal(LobbyAuthenticationBuilder.HostId, lobby.HostUserId);
        }

        [Fact]
        public void Create_GuestWithoutToken_Accepts()
        {
            LobbyAuthenticationBuilder setup = new LobbyAuthenticationBuilder();

            LobbyDetails lobby = setup.CreateGuestLobby();

            Assert.Equal(LobbyAuthenticationBuilder.GuestId, lobby.HostUserId);
        }

        [Fact]
        public void Create_PositiveIdentifierClaimingGuest_RejectsMissingToken()
        {
            LobbyAuthenticationBuilder setup = new LobbyAuthenticationBuilder();

            RpcException exception = setup.CreateWithoutToken();

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }

        [Fact]
        public void Join_TokenBelongsToAnotherUser_Rejects()
        {
            LobbyAuthenticationBuilder setup = new LobbyAuthenticationBuilder();

            RpcException exception = setup.JoinWithAnotherUsersToken();

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }

        [Fact]
        public void Leave_RevokedToken_Rejects()
        {
            LobbyAuthenticationBuilder setup = new LobbyAuthenticationBuilder();

            RpcException exception = setup.LeaveWithRevokedToken();

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }

        [Fact]
        public void Start_MissingToken_Rejects()
        {
            LobbyAuthenticationBuilder setup = new LobbyAuthenticationBuilder();

            RpcException exception = setup.StartWithoutToken();

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }

        [Fact]
        public void Friends_MissingToken_Rejects()
        {
            LobbyAuthenticationBuilder setup = new LobbyAuthenticationBuilder();

            RpcException exception = setup.ReadFriendsWithoutToken();

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }

        [Fact]
        public void Invite_MissingToken_Rejects()
        {
            LobbyAuthenticationBuilder setup = new LobbyAuthenticationBuilder();

            RpcException exception = setup.InviteWithoutToken();

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }

        [Fact]
        public void Chat_MissingToken_Rejects()
        {
            LobbyAuthenticationBuilder setup = new LobbyAuthenticationBuilder();

            RpcException exception = setup.SendWithoutToken();

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }
    }
}
