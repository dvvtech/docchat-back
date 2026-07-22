namespace DocChat.Api.Models.Documents
{
    public sealed record DocumentChunkDto(
        int Index,
        int CharacterCount,
        string Preview);
}
