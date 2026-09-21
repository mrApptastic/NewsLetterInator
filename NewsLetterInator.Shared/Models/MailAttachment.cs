namespace NewsLetterInator.Shared.Models;

public sealed class MailAttachment
{
    public string? DriveFileId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    public bool IsStoredOnDrive { get; set; }

    public byte[] Content { get; set; } = [];
}