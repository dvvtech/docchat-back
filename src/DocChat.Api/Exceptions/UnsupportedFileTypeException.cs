namespace DocChat.Api.Exceptions;

public sealed class UnsupportedFileTypeException : ApiException
{
    public UnsupportedFileTypeException(string message)
        : base(StatusCodes.Status415UnsupportedMediaType, message)
    {
    }
}
