using System;

namespace Orchestra.Application.Common.Exceptions
{
    public class SummarizationException : Exception
    {
        public SummarizationException(string message)
            : base(message)
        {
        }

        public SummarizationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
