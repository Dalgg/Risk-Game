using RiskGame.Models;

namespace RiskGame.Services
{
    public static class SessionService
    {
        public static SessionUser? CurrentUser { get; private set; }

        public static bool IsLoggedIn => CurrentUser != null;

        public static void Login(User user)
        {
            CurrentUser = new SessionUser( user.IdUser, user.Username, user.Nickname, user.AvatarReference);
        }
        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}