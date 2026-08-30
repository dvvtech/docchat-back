namespace DocChat.Api.Exceptions;

public sealed class DocumentProcessingException : ApiException
{
    public DocumentProcessingException(string message)
        : base(StatusCodes.Status422UnprocessableEntity, message)
    {
    }

    public DocumentProcessingException(string message, Exception innerException)
        : base(StatusCodes.Status422UnprocessableEntity, message, innerException)
    {
    }
}
