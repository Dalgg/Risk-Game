using System;

namespace RiskGame.Exceptions
{
    public class LobbyFullException : Exception
    {
        public LobbyFullException(string message) : base(message)
        {
        }

        public LobbyFullException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
