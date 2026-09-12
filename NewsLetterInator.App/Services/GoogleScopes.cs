namespace NewsLetterInator.App.Services;

public static class GoogleScopes
{
    public const string OpenId = "openid";
    public const string Profile = "https://www.googleapis.com/auth/userinfo.profile";
    public const string Email = "https://www.googleapis.com/auth/userinfo.email";
    public const string Sheets = "https://www.googleapis.com/auth/spreadsheets";
    public const string GmailSend = "https://www.googleapis.com/auth/gmail.send";
    public const string DriveFile = "https://www.googleapis.com/auth/drive.file";

    public static readonly string[] RequiredForPoc =
    [
        OpenId,
        Profile,
        Email,
        Sheets,
        GmailSend,
        DriveFile
    ];
}
