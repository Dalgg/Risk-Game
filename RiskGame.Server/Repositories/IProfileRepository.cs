using RiskGame.Contracts.Profile;

namespace RiskGame.Server.Services
{
    public interface IProfileRepository
    {
        UserProfile GetProfile(int userId);
    }
}
