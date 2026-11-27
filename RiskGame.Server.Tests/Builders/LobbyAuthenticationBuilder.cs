using System;
using Grpc.Core;
using RiskGame.Contracts.Chat;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class LobbyAuthenticationBuilder
    {
        public const int HostId = 41;
        public const int OtherId = 43;
        public const int GuestId = -41;
        private const int TurnSeconds = 60;
        private const int InvalidUserIdentifier = 0;
        private const string LobbyCode = "RULES1";
        private const string Nickname = "Session player";
        private const string ChatText = "Test message";
        private readonly ProfileSessionRegistry registry = new ProfileSessionRegistry();

        public LobbyAuthenticationBuilder()
        {
            LobbySessionManager manager = new LobbySessionManager(new LobbySettingsBuilder.SettingsRepository(),
                registry, new MatchSessionManager(registry, new RecordingMatchRepository()));
            Lobby = new LobbyGrpcService(manager);
            Chat = new ChatGrpcService(manager);
            Token = registry.Register(HostId);
        }

        public LobbyGrpcService Lobby { get; }

        public ChatGrpcService Chat { get; }

        public string Token { get; }

        public LobbyDetails CreateRegisteredLobby()
        {
            return Lobby.CreateLobby(CreateRequest(HostId, Token), null!).GetAwaiter().GetResult();
        }

        public LobbyDetails CreateGuestLobby()
        {
            return Lobby.CreateLobby(CreateRequest(GuestId, string.Empty), null!).GetAwaiter().GetResult();
        }

        public RpcException CreateWithoutToken()
        {
            CreateLobbyRequest request = CreateRequest(HostId, string.Empty);
            request.Player.IsGuest = true;
            return Failure(() => Lobby.CreateLobby(request, null!).GetAwaiter().GetResult());
        }

        public RpcException JoinWithAnotherUsersToken()
        {
            JoinLobbyRequest request = new JoinLobbyRequest
            {
                LobbyCode = LobbyCode, Player = Player(OtherId), SessionToken = Token
            };
            return Failure(() => Lobby.JoinLobby(request, null!).GetAwaiter().GetResult());
        }

        public RpcException LeaveWithRevokedToken()
        {
            registry.RemoveSession(Token);
            UserLobbyRequest request = new UserLobbyRequest { UserId = HostId, SessionToken = Token };
            return Failure(() => Lobby.LeaveLobby(request, null!).GetAwaiter().GetResult());
        }

        public RpcException StartWithoutToken()
        {
            UserLobbyRequest request = new UserLobbyRequest { UserId = HostId };
            return Failure(() => Lobby.StartMatch(request, null!).GetAwaiter().GetResult());
        }

        public RpcException ReadFriendsWithoutToken()
        {
            UserLobbyRequest request = new UserLobbyRequest { UserId = HostId };
            return Failure(() => Lobby.GetFriends(request, null!).GetAwaiter().GetResult());
        }

        public RpcException InviteWithoutToken()
        {
            InviteFriendRequest request = new InviteFriendRequest { SenderUserId = HostId, RecipientUserId = OtherId };
            return Failure(() => Lobby.InviteFriend(request, null!).GetAwaiter().GetResult());
        }

        public RpcException SendWithoutToken()
        {
            SendChatRequest request = new SendChatRequest
            {
                Message = new ChatMessage { Sender = Player(HostId), Text = ChatText }
            };
            return Failure(() => Chat.SendMessage(request, null!).GetAwaiter().GetResult());
        }

        private static CreateLobbyRequest CreateRequest(int userId, string token)
        {
            return new CreateLobbyRequest
            {
                Player = Player(userId), SessionToken = token,
                Rules = new LobbyRules { TurnDurationSeconds = TurnSeconds }
            };
        }

        private static PlayerSummary Player(int userId)
        {
            return new PlayerSummary { UserId = userId, Nickname = Nickname, IsGuest = userId < InvalidUserIdentifier };
        }

        private static RpcException Failure(Action operation)
        {
            return (RpcException)Record.Exception(operation)!;
        }
    }
}
