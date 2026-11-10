namespace RiskGame.Exceptions
{
    public sealed class InvalidCredentialsException : DataOperationException
    {
        public InvalidCredentialsException(string message) : base(message)
        {
        }
    }
}
