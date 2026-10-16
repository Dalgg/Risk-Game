using System;

namespace RiskGame.Exceptions
{
    public class DataOperationException : Exception
    {
        public DataOperationException(string message, Exception innerException) 
            : base(message, innerException) 
        { 
        }

        public DataOperationException(string message) 
            : base(message) 
        { 
        }
    }
}
