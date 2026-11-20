using RiskGame.Contracts.Profile;

namespace RiskGame.Server.Services
{
    public interface IProfileRepository
    {
        UserProfile GetProfile(int userId);
        UserProfile UpdateNickname(int userId, string nickname);
        UserProfile UpdateAvatar(int userId, string avatarReference);
    }
}
