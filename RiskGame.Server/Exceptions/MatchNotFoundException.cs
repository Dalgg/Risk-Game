namespace RiskGame.Exceptions
{
    public sealed class MatchNotFoundException : DataOperationException
    {
        public MatchNotFoundException(string message) : base(message)
        {
        }
    }
}
