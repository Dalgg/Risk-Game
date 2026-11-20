using System;
using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Profile;
using RiskGame.Exceptions;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class ProfileGrpcService : Profile.ProfileBase
    {
        private const int MinimumRegisteredIdentifier = 1;
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

        public override Task<UserProfile> UpdateNickname(UpdateNicknameRequest request, ServerCallContext context)
        {
            try
            {
                int userId = AuthenticateUser(request.UserId, request.SessionToken);
                return Task.FromResult(profileRepository.UpdateNickname(userId, request.Nickname));
            }
            catch (ArgumentException argumentException)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, argumentException.Message));
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateUpdateFailure(dataOperationException);
            }
        }

        public override Task<UserProfile> UpdateAvatar(UpdateAvatarRequest request, ServerCallContext context)
        {
            try
            {
                int userId = AuthenticateUser(request.UserId, request.SessionToken);
                return Task.FromResult(profileRepository.UpdateAvatar(userId, request.AvatarReference));
            }
            catch (ArgumentException argumentException)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, argumentException.Message));
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateUpdateFailure(dataOperationException);
            }
        }

        private int AuthenticateUser(int requestedUserId, string token)
        {
            int userId = profileSessions.GetUserId(token);
            ValidateUserIdentity(requestedUserId, userId);
            if (userId < MinimumRegisteredIdentifier)
            {
                throw new GuestNotAllowedException("A registered account is required to edit the profile.");
            }
            return userId;
        }

        private static RpcException CreateUpdateFailure(DataOperationException exception)
        {
            bool expectedFailure = exception is InvalidSessionException || exception is ProfileNotFoundException;
            if (!expectedFailure && exception is not GuestNotAllowedException)
            {
                JsonLogger.Error(exception, "Could not update the player profile.");
            }
            return CreateRpcException(exception);
        }

        private static void ValidateUserIdentity(int requestedUserId, int authenticatedUserId)
        {
            if (requestedUserId != authenticatedUserId)
            {
                throw new InvalidSessionException("The requested user does not match the active session.");
            }
        }

        private static RpcException CreateRpcException(DataOperationException dataOperationException)
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
            if (dataOperationException is GuestNotAllowedException)
            {
                statusCode = StatusCode.PermissionDenied;
            }
            return new RpcException(new Status(statusCode, dataOperationException.Message));
        }
    }
}
