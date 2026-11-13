using RiskGame.Contracts.Profile;

namespace RiskGame.Server.Services
{
    public interface IProfileRepository
    {
        /// <summary>Retrieves the registered player's profile and persisted match statistics.</summary>
        /// <param name="userId">The authenticated registered user identifier.</param>
        /// <returns>The registered player's profile.</returns>
        /// <exception cref="RiskGame.Exceptions.ProfileNotFoundException">The account does not exist.</exception>
        UserProfile GetProfile(int userId);
    }
}
