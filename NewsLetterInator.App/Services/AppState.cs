using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.App.Services;

public sealed class AppState
{
    private readonly Dictionary<string, Person> selectedPeopleByEmail = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public SheetInfo? SelectedSheet { get; private set; }

    public IReadOnlyList<Person> SelectedPeople => selectedPeopleByEmail.Values.ToList();

    public void SetSelectedSheet(SheetInfo? sheet)
    {
        SelectedSheet = sheet;
        Changed?.Invoke();
    }

    public bool IsPersonSelected(string email)
    {
        return selectedPeopleByEmail.ContainsKey(email);
    }

    public void SetPersonSelected(Person person, bool isSelected)
    {
        if (string.IsNullOrWhiteSpace(person.Email))
        {
            return;
        }

        if (isSelected)
        {
            selectedPeopleByEmail[person.Email] = person;
        }
        else
        {
            selectedPeopleByEmail.Remove(person.Email);
        }

        Changed?.Invoke();
    }

    public void ClearSelectedPeople()
    {
        selectedPeopleByEmail.Clear();
        Changed?.Invoke();
    }
}
