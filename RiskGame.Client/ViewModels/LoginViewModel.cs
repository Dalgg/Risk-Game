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
        public SessionUser AuthenticateUser(string username, string password)
        {
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                AuthGrpc.AuthGrpcClient authClient = new AuthGrpc.AuthGrpcClient(grpcChannel);
                LoginResponse loginResponse = authClient.Login(new LoginRequest
                {
                    Username = username,
                    Password = password
                });

                if (loginResponse.IsSuspended)
                {
                    throw new DataOperationException(
                        AccountSuspensionMessage.Format(loginResponse.BannedUntilUnixMilliseconds));
                }

                SessionUser authenticatedUser;
                if (loginResponse.IsAuthenticated && loginResponse.Player != null)
                {
                    string? avatarReference = null;
                    if (!string.IsNullOrWhiteSpace(loginResponse.Player.AvatarReference))
                    {
                        avatarReference = loginResponse.Player.AvatarReference;
                    }

                    authenticatedUser = new SessionUser(
                        loginResponse.Player.UserId,
                        loginResponse.Player.Username,
                        loginResponse.Player.Nickname)
                    {
                        AvatarReference = avatarReference,
                        SessionToken = loginResponse.SessionToken
                    };
                }
                else
                {
                    throw new DataOperationException(Strings.LoginErrorInvalidAuth);
                }

                return authenticatedUser;
            }
            catch (RpcException rpcException)
            {
                string message = Strings.LoginErrorDatabase;
                if (rpcException.StatusCode == StatusCode.FailedPrecondition)
                {
                    message = Strings.LoginErrorSessionAlreadyActive;
                }
                throw new DataOperationException(message, rpcException);
            }
        }
    }
}
