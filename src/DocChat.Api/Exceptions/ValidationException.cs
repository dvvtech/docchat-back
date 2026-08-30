namespace DocChat.Api.Exceptions;

public sealed class ValidationException : ApiException
{
    public ValidationException(string message)
        : base(StatusCodes.Status400BadRequest, message)
    {
    }
}
