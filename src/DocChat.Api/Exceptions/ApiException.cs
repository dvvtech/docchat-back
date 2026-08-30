namespace DocChat.Api.Exceptions;

public abstract class ApiException : Exception
{
    public int StatusCode { get; }

    protected ApiException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    protected ApiException(int statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
