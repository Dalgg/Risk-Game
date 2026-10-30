using System;

namespace RiskGame.Exceptions
{
    public class LobbyNotFoundException : Exception
    {
        public LobbyNotFoundException(string message) : base(message)
        {
        }

        public LobbyNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
