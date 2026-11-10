using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Profile;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class ProfileGrpcService : Profile.ProfileBase
    {
        private readonly IProfileRepository profileRepository;
        private readonly ProfileSessionRegistry profileSessions;

        public ProfileGrpcService(IProfileRepository repository, ProfileSessionRegistry sessions)
        {
            profileRepository = repository;
            profileSessions = sessions;
        }

        public override Task<UserProfile> GetProfile(ProfileQuery profileQuery, ServerCallContext callContext)
        {
            try
            {
                int userId = profileSessions.GetUserId(profileQuery.SessionToken);
                ValidateUserIdentity(profileQuery.UserId, userId);
                return Task.FromResult(profileRepository.GetProfile(userId));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not retrieve the requested profile.");
                throw CreateRpcException(dataOperationException);
            }
        }

        private void ValidateUserIdentity(int requestedUserId, int authenticatedUserId)
        {
            if (requestedUserId != authenticatedUserId)
            {
                throw new InvalidSessionException("The requested user does not match the active session.");
            }
        }

        private RpcException CreateRpcException(DataOperationException dataOperationException)
        {
            StatusCode statusCode = StatusCode.Unavailable;
            if (dataOperationException is InvalidSessionException)
            {
                statusCode = StatusCode.Unauthenticated;
            }
            if (dataOperationException is ProfileNotFoundException)
            {
                statusCode = StatusCode.NotFound;
            }
            return new RpcException(new Status(statusCode, dataOperationException.Message));
        }
    }
}
