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

    [Fact]
    public void SetSelectedSheet_RaisesChangedEvent()
    {
        var state = new AppState();
        var changedEventRaised = false;
        state.Changed += () => changedEventRaised = true;

        state.SetSelectedSheet(new SheetInfo { Id = "sheet-1", Name = "People" });

        Assert.True(changedEventRaised);
    }

    [Fact]
    public void ClearSelectedPeople_RaisesChangedEvent()
    {
        var state = new AppState();
        var changedEventRaised = false;
        state.Changed += () => changedEventRaised = true;
        state.SetPersonSelected(new Person { Name = "Active", Email = "active@example.com", Active = true }, true);

        changedEventRaised = false;
        state.ClearSelectedPeople();

        Assert.True(changedEventRaised);
    }
}
