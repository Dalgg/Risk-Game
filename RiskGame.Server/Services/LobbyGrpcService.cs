using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class LobbyGrpcService : Lobby.LobbyBase
    {
        private const string FaultCodeTrailerKey = "fault-code";
        private const string UserIdHeaderKey = "user-id";
        private const string NicknameHeaderKey = "nickname";
        private const string AvatarHeaderKey = "avatar-reference";
        private const string IsGuestHeaderKey = "is-guest";
        private readonly LobbySessionManager _sessionManager;

        public LobbyGrpcService(LobbySessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public override Task<LobbyDetails> JoinLobby(StringValue request, ServerCallContext context)
        {
            try
            {
                PlayerSummary player = ExtractPlayerFromHeaders(context.RequestHeaders);
                LobbyDetails lobbyDetails = _sessionManager.JoinLobby(request.Value, player);
                return Task.FromResult(lobbyDetails);
            }
            catch (LobbyNotFoundException ex)
            {
                throw CreateFaultException(StatusCode.NotFound, FaultCode.LobbyNotFound, ex.Message);
            }
            catch (LobbyFullException ex)
            {
                throw CreateFaultException(StatusCode.ResourceExhausted, FaultCode.LobbyFull, ex.Message);
            }
            catch (DataOperationException ex)
            {
                throw CreateFaultException(StatusCode.Unavailable, FaultCode.DatabaseUnavailable, ex.Message);
            }
        }

        public override Task<Empty> LeaveLobby(Empty request, ServerCallContext context)
        {
            PlayerSummary player = ExtractPlayerFromHeaders(context.RequestHeaders);
            _sessionManager.LeaveLobby(player.UserId);
            return Task.FromResult(new Empty());
        }

        public override async Task SubscribeLobbyEvents(
            Empty request,
            IServerStreamWriter<LobbyEvent> responseStream,
            ServerCallContext context)
        {
            PlayerSummary player = ExtractPlayerFromHeaders(context.RequestHeaders);
            LobbyDetails? initialSnapshot = _sessionManager.RegisterSubscriber(player.UserId, responseStream);

            if (initialSnapshot != null)
            {
                LobbyEvent snapshotEvent = new LobbyEvent
                {
                    LobbyChanged = new LobbyChanged
                    {
                        Change = LobbyChangeType.Snapshot,
                        Lobby = initialSnapshot,
                        AffectedUserId = player.UserId
                    }
                };

                await responseStream.WriteAsync(snapshotEvent);
            }

            TaskCompletionSource<bool> disconnectSignal = new TaskCompletionSource<bool>();
            using (context.CancellationToken.Register(() => disconnectSignal.TrySetResult(true)))
            {
                await disconnectSignal.Task;
            }

            _sessionManager.UnregisterSubscriber(player.UserId);
        }

        private static PlayerSummary ExtractPlayerFromHeaders(Metadata headers)
        {
            int userId = 0;
            string nickname = "Guest";
            string avatarReference = string.Empty;
            bool isGuest = true;

            foreach (Metadata.Entry entry in headers)
            {
                if (string.Equals(entry.Key, UserIdHeaderKey, StringComparison.OrdinalIgnoreCase))
                {
                    _ = int.TryParse(entry.Value, out userId);
                }
                else if (string.Equals(entry.Key, NicknameHeaderKey, StringComparison.OrdinalIgnoreCase))
                {
                    nickname = entry.Value;
                }
                else if (string.Equals(entry.Key, AvatarHeaderKey, StringComparison.OrdinalIgnoreCase))
                {
                    avatarReference = entry.Value;
                }
                else if (string.Equals(entry.Key, IsGuestHeaderKey, StringComparison.OrdinalIgnoreCase))
                {
                    _ = bool.TryParse(entry.Value, out isGuest);
                }
            }

            if (userId == 0)
            {
                userId = Random.Shared.Next(100000, 999999);
            }

            return new PlayerSummary
            {
                UserId = userId,
                Nickname = nickname,
                AvatarReference = avatarReference,
                IsGuest = isGuest
            };
        }

        private static RpcException CreateFaultException(StatusCode statusCode, FaultCode faultCode, string message)
        {
            Metadata trailers = new Metadata
            {
                { FaultCodeTrailerKey, faultCode.ToString() }
            };

            return new RpcException(new Status(statusCode, message), trailers);
        }
    }
}
