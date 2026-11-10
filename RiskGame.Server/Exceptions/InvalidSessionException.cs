namespace RiskGame.Exceptions
{
    public sealed class InvalidSessionException : DataOperationException
    {
        public InvalidSessionException(string message) : base(message)
        {
        }
    }
}
