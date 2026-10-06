namespace RiskGame.Models
{
    public enum RegisterResult
    {
        Success,
        EmptyFields,
        InvalidEmail,
        InvalidUsername,
        WeakPassword,
        UsernameExists,
        EmailExists
    }
}