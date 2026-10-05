namespace ReservationWebAPI.Application.Helpers
{

    public static class ExceptionHelper 
    {
        public static CustomException FromException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                CustomException custom => custom,

                ArgumentException argument => new CustomException(
                    argument.Message,
                    CustomException.ExceptionType.InvalidArgument,
                    details: argument.ParamName is { } name
                        ? $"Parameter: {name}"
                        : null,
                    innerException: argument),

                _ => new CustomException(
                    exception.Message,
                    CustomException.ExceptionType.Unknown,
                    innerException: exception)
            };
        }
    }

    public class CustomException : Exception
    {
        public enum ExceptionType
        {
            Unknown = 0,
            ObjectNotFound = 1,
            InvalidArgument = 2,
            SerializationError = 3,
            InvalidOperation = 4,
        }

        public ExceptionType Type { get; set; }
        public string? Details { get; set; } = null!;

        public CustomException() : this("An unexpected error occurred.") { }
        public CustomException(string message) : this(message, ExceptionType.Unknown) { }

        public CustomException(string message, Exception innerException) : this(message, ExceptionType.Unknown, innerException: innerException) { }

        public CustomException(string message, ExceptionType type, string? details = null, Exception? innerException = null) : base(message, innerException)
        {
            Type = type;
            Details = details;
        }
    }
}
