namespace RiskGame.Exceptions
{
    public sealed class SessionAlreadyActiveException : DataOperationException
    {
        public SessionAlreadyActiveException(string message) : base(message)
        {
        }
    }
}
