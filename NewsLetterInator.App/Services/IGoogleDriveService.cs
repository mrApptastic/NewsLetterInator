using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.App.Services;

public interface IGoogleDriveService
{
    Task<IReadOnlyList<SheetInfo>> GetSheetsAsync();
}
