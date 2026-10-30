using System.Security.Cryptography;

namespace RiskGame.Services
{
    public static class SessionService
    {
        private const int bottomUserIdRange = 1000;
        private const int topUserIdRange = 9999;

        public static SessionUser? CurrentUser { get; private set; }

        public static bool IsLoggedIn => CurrentUser != null;

        public static void Login(SessionUser user)
        {
            CurrentUser = user;
        }

        public static void Login(int idUser, string username, string nickname, string? avatarReference)
        {
            CurrentUser = new SessionUser(idUser, username, nickname, avatarReference);
        }

        public static SessionUser GetOrCreateCurrentUser()
        {
            if (CurrentUser == null)
            {
                int guestNumber = RandomNumberGenerator.GetInt32(bottomUserIdRange, topUserIdRange);
                string guestAlias = $"Invitado_{guestNumber}";
                CurrentUser = new SessionUser(-guestNumber, guestAlias, guestAlias, null);
            }

            return CurrentUser;
        }

        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}
