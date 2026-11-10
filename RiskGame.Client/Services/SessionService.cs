using System.Security.Cryptography;

namespace RiskGame.Services
{
    public static class SessionService
    {
        private const int BottomUserIdRange = 1000;
        private const int TopUserIdRange = 9999;
        private const string GuestPrefix = "Invitado_";

        public static SessionUser? CurrentUser { get; private set; }

        public static bool IsLoggedIn => CurrentUser != null;

        public static void Login(SessionUser user)
        {
            CurrentUser = user;
        }

        public static SessionUser GetOrCreateCurrentUser()
        {
            if (CurrentUser == null)
            {
                int guestNumber = RandomNumberGenerator.GetInt32(BottomUserIdRange, TopUserIdRange);
                string guestAlias = $"{GuestPrefix}{guestNumber}";
                CurrentUser = new SessionUser(-guestNumber, guestAlias, guestAlias);
            }

            return CurrentUser;
        }

        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}
