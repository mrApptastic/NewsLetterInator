using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.App.Services;

public sealed class AppState
{
    private readonly Dictionary<string, Person> selectedPeopleByEmail = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public GoogleUser? CurrentUser { get; private set; }

    public SheetInfo? SelectedSheet { get; private set; }

    public IReadOnlyList<Person> SelectedPeople => selectedPeopleByEmail.Values.Where(person => person.Active).ToList();

    public bool IsSignedIn => CurrentUser is not null;

    public void SetSignedInUser(GoogleUser? user)
    {
        CurrentUser = user;
        Changed?.Invoke();
    }

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

        if (!person.Active)
        {
            selectedPeopleByEmail.Remove(person.Email);
            Changed?.Invoke();
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

    public void ResetForSignOut()
    {
        CurrentUser = null;
        SelectedSheet = null;
        selectedPeopleByEmail.Clear();
        Changed?.Invoke();
    }
}
