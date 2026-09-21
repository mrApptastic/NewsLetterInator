using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.App.Services;

public interface IGoogleDriveService
{
    Task<IReadOnlyList<SheetInfo>> GetSheetsAsync();

    Task EnsureSheetInAppFolderAsync(string spreadsheetId);

    Task<MailAttachment> UploadTemplateAttachmentAsync(string spreadsheetId, string templateName, MailAttachment attachment);

    Task<MailAttachment> GetAttachmentAsync(string fileId, bool includeContent);
}
