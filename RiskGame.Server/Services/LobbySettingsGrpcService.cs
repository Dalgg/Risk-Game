using System;
using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Lobby;
using RiskGame.Contracts.LobbySettings;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class LobbySettingsGrpcService : LobbySettings.LobbySettingsBase
    {
        private readonly LobbySessionManager sessionManager;

        public LobbySettingsGrpcService(LobbySessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public override Task<LobbyDetails> UpdateRules(UpdateLobbyRulesRequest request, ServerCallContext callContext)
        {
            try
            {
                return Task.FromResult(sessionManager.UpdateRules(request));
            }
            catch (ArgumentException argumentException)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, argumentException.Message));
            }
            catch (LobbyNotFoundException lobbyNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, lobbyNotFoundException.Message));
            }
            catch (InvalidOperationException invalidOperationException)
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition, invalidOperationException.Message));
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateFailure(dataOperationException);
            }
        }

        private static RpcException CreateFailure(DataOperationException exception)
        {
            StatusCode status = StatusCode.Unavailable;
            if (exception is InvalidSessionException)
            {
                status = StatusCode.Unauthenticated;
            }
            else
            {
                JsonLogger.Error(exception, "Could not persist lobby settings.");
            }
            return new RpcException(new Status(status, "The lobby settings could not be updated."));
        }
    }
}
