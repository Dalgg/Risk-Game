using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Callbacks;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public class LobbyViewModel : ViewModelBase
    {
        private const string UserIdHeaderKey = "user-id";
        private const string NicknameHeaderKey = "nickname";
        private const string AvatarHeaderKey = "avatar-reference";
        private const string IsGuestHeaderKey = "is-guest";
        private CancellationTokenSource? _streamCancellation;

        public LobbyDetails JoinLobby(string roomCode)
        {
            try
            {
                GrpcChannel channel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient client = new Lobby.LobbyClient(channel);
                Metadata headers = BuildPlayerHeaders();
                return client.JoinLobby(new StringValue { Value = roomCode }, headers);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                throw new LobbyNotFoundException("No se encontró ninguna sala activa con ese código.", ex);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.ResourceExhausted)
            {
                throw new LobbyFullException("La sala ya está llena (máximo 4 jugadores).", ex);
            }
            catch (RpcException ex)
            {
                JsonLogger.Error(ex, "gRPC communication error (RpcException) while joining lobby {0}.", roomCode);
                throw new DataOperationException("No se pudo conectar con el servidor de salas.", ex);
            }
        }

        public void LeaveLobby()
        {
            StopSubscription();
            try
            {
                GrpcChannel channel = GrpcChannelProvider.GetChannel();
                Lobby.LobbyClient client = new Lobby.LobbyClient(channel);
                Metadata headers = BuildPlayerHeaders();
                _ = client.LeaveLobby(new Empty(), headers);
            }
            catch (RpcException ex)
            {
                JsonLogger.Error(ex, "gRPC communication error (RpcException) while leaving lobby.");
            }
        }

        public void SubscribeToEvents(ILobbyCallback callback)
        {
            StopSubscription();
            _streamCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = _streamCancellation.Token;
            Task.Run(async () =>
            {
                try
                {
                    GrpcChannel channel = GrpcChannelProvider.GetChannel();
                    Lobby.LobbyClient client = new Lobby.LobbyClient(channel);
                    Metadata headers = BuildPlayerHeaders();

                    using AsyncServerStreamingCall<LobbyEvent> call = client.SubscribeLobbyEvents(
                        new Empty(),
                        headers,
                        cancellationToken: cancellationToken);

                    while (await call.ResponseStream.MoveNext(cancellationToken))
                    {
                        LobbyEvent currentEvent = call.ResponseStream.Current;
                        DispatchLobbyEvent(currentEvent, callback);
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                }
                catch (OperationCanceledException)
                {
                }
                catch (RpcException ex)
                {
                    JsonLogger.Error(ex, "Lobby event stream disconnected with RpcException.");
                }
            });
        }

        public void StopSubscription()
        {
            if (_streamCancellation != null)
            {
                _streamCancellation.Cancel();
                _streamCancellation.Dispose();
                _streamCancellation = null;
            }
        }

        private static void DispatchLobbyEvent(LobbyEvent currentEvent, ILobbyCallback callback)
        {
            switch (currentEvent.PayloadCase)
            {
                case LobbyEvent.PayloadOneofCase.LobbyChanged:
                    callback.LobbyChanged(currentEvent.LobbyChanged);
                    break;
                case LobbyEvent.PayloadOneofCase.LobbyClosed:
                    callback.LobbyClosed(currentEvent.LobbyClosed);
                    break;
                case LobbyEvent.PayloadOneofCase.MatchStarting:
                    callback.MatchStarting(currentEvent.MatchStarting);
                    break;
                default:
                    break;
            }
        }

        private static Metadata BuildPlayerHeaders()
        {
            SessionUser currentUser = SessionService.GetOrCreateCurrentUser();
            bool isGuest = currentUser.IdUser < 0;
            return new Metadata
            {
                { UserIdHeaderKey, currentUser.IdUser.ToString() },
                { NicknameHeaderKey, currentUser.Nickname },
                { AvatarHeaderKey, currentUser.AvatarReference ?? string.Empty },
                { IsGuestHeaderKey, isGuest.ToString() }
            };
        }
    }
}
