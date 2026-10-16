using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Auth;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        public SessionUser? AuthenticateUser(string username, string password)
        {
            try
            {
                GrpcChannel channel = GrpcChannelProvider.GetChannel();
                AuthGrpc.AuthGrpcClient client = new AuthGrpc.AuthGrpcClient(channel);
                LoginResponse response = client.Login(new LoginRequest
                {
                    Username = username,
                    Password = password
                });

                if (!response.IsAuthenticated || response.Player == null)
                {
                    return null;
                }

                string? avatar = string.IsNullOrWhiteSpace(response.Player.AvatarReference)
                    ? null
                    : response.Player.AvatarReference;

                return new SessionUser(
                    response.Player.UserId,
                    response.Player.Username,
                    response.Player.Nickname,
                    avatar);
            }
            catch (RpcException ex)
            {
                JsonLogger.Error(ex, "gRPC communication error (RpcException) during login for user {0}.", username);
                throw new DataOperationException(Strings.LoginErrorDatabase, ex);
            }
        }
    }
}