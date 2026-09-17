using NewsLetterInator.App.Services;
using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.Tests;

public class AppStateTests
{
    [Fact]
    public void SetPersonSelected_WhenPersonIsInactive_DoesNotSelectThem()
    {
        var state = new AppState();
        var person = new Person { Name = "Inactive", Email = "inactive@example.com", Active = false };

        state.SetPersonSelected(person, true);

        Assert.False(state.IsPersonSelected(person.Email));
        Assert.Empty(state.SelectedPeople);
    }

    [Fact]
    public void SelectedPeople_OnlyIncludesActivePeople()
    {
        var state = new AppState();

        state.SetPersonSelected(new Person { Name = "Active", Email = "active@example.com", Active = true }, true);
        state.SetPersonSelected(new Person { Name = "Inactive", Email = "inactive@example.com", Active = false }, true);

        Assert.Single(state.SelectedPeople);
        Assert.Equal("active@example.com", state.SelectedPeople[0].Email);
    }
}
