using Grpc.Core;
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
                _ = GrpcChannelProvider.GetChannel();
                return null;
            }
            catch (RpcException ex)
            {
                JsonLogger.Error(ex, "gRPC communication error (RpcException) during login for user {0}.", username);
                throw new DataOperationException(Strings.LoginErrorDatabase, ex);
            }
        }
    }
}
