namespace RiskGame.Exceptions
{
    public class InvalidSessionException : DataOperationException
    {
        public InvalidSessionException(string message) : base(message)
        {
        }
    }
}
