using System;

namespace Orchestra.Application.Common.Exceptions
{
    public class DuplicateEmailException : Exception
    {
        public DuplicateEmailException(string email)
            : base($"User with email '{email}' already exists.")
        {
        }
    }
}