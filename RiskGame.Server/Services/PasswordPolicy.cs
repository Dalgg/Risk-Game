namespace RiskGame.Services
{
    public static class PasswordPolicy
    {
        public const int MinLength = 8;

        public static bool  PasswordIsValid(string password)
        {
            if (password.Length < MinLength)
            {
                return false;
            }

            bool hasUpper = false;
            bool hasLower = false;
            bool hasDigit = false;
            bool hasSpecial = false;

            foreach (char character in password)
            {
                if (char.IsUpper(character))
                {
                    hasUpper = true;
                }
                else if (char.IsLower(character))
                {
                    hasLower = true;
                }
                else if (char.IsDigit(character))
                {
                    hasDigit = true;
                }
                else if (!char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character))
                {
                    hasSpecial = true;
                }
            }

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }
    }
}