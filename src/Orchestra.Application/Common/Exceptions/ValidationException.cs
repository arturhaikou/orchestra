using System;

namespace Orchestra.Application.Common.Exceptions
{
    public class ValidationException : Exception
    {
        public Dictionary<string, string[]> Errors { get; }

        public ValidationException(Dictionary<string, string[]> errors)
            : base(BuildMessage(errors))
        {
            Errors = errors ?? new Dictionary<string, string[]>();
        }

        private static string BuildMessage(Dictionary<string, string[]> errors)
        {
            if (errors is null || errors.Count == 0)
                return "One or more validation errors occurred.";

            var details = string.Join(" ", errors.SelectMany(kv =>
                kv.Value.Select(v => $"{kv.Key}: {v}")));
            return $"One or more validation errors occurred. {details}";
        }
    }
}