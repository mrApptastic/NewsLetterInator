namespace NewsLetterInator.Shared.Models;

public sealed class Person
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool Active { get; set; } = true;
}
