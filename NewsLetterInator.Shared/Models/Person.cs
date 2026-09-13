namespace NewsLetterInator.Shared.Models;

public sealed class Person
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string CustomField_1 { get; set; } = string.Empty;
    public string CustomField_2 { get; set; } = string.Empty;
    public string CustomField_3 { get; set; } = string.Empty;
    public string CustomField_4 { get; set; } = string.Empty;
    public string CustomField_5 { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}
