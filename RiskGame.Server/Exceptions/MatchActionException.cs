namespace RiskGame.Exceptions
{
    public sealed class MatchActionException : DataOperationException
    {
        public MatchActionException(string message) : base(message)
        {
        }
    }
}
