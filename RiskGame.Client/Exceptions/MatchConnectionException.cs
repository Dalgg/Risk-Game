using System;

namespace RiskGame.Exceptions
{
    public sealed class MatchConnectionException : DataOperationException
    {
        public MatchConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
