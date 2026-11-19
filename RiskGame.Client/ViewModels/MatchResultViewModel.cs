using System;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed class MatchResultViewModel : ViewModelBase
    {
        private const int RequestTimeoutSeconds = 5;
        private readonly SessionUser session = SessionService.GetOrCreateCurrentUser();

        public LobbyDetails ReturnToLobby(int matchId)
        {
            ReturnToLobbyRequest request = new ReturnToLobbyRequest
            {
                MatchId = matchId, UserId = session.IdUser, SessionToken = session.SessionToken
            };
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient client = new Lobby.LobbyClient(grpcChannel);
                return client.ReturnToLobby(request, deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not return from the match result to the original lobby.");
                throw new DataOperationException(Strings.MatchResultReturnError, rpcException);
            }
        }

        public void LeaveLobby()
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient client = new Lobby.LobbyClient(grpcChannel);
                client.LeaveLobby(new UserLobbyRequest { UserId = session.IdUser },
                    deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not leave the lobby when closing the match result.");
            }
        }
    }
}
