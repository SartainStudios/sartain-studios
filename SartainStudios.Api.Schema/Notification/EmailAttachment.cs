namespace SartainStudios.Api.Schema.Notification;

public sealed class EmailAttachment
{
    public EmailAttachment(string fileName, byte[] content, string contentType)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentException("FileName cannot be null or empty.");
        if (content == null || content.Length == 0)
            throw new ArgumentException("Content cannot be null or empty.");
        if (string.IsNullOrEmpty(contentType))
            throw new ArgumentException("ContentType cannot be null or empty.");
        FileName = fileName;
        Content = content;
        ContentType = contentType;
    }

    public string FileName { get; }
    public byte[] Content { get; }
    public string ContentType { get; }
}