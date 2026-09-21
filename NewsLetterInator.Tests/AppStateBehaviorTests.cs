using NewsLetterInator.App.Services;
using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.Tests;

public class AppStateBehaviorTests
{
    [Fact]
    public void SetPersonSelected_WhenSelectedAndThenDeselected_RemovesPerson()
    {
        var state = new AppState();
        var person = new Person { Name = "A", Email = "a@example.com", Active = true };

        state.SetPersonSelected(person, true);
        state.SetPersonSelected(person, false);

        Assert.False(state.IsPersonSelected(person.Email));
        Assert.Empty(state.SelectedPeople);
    }

    [Fact]
    public void ClearSelectedPeople_RemovesAllSelections()
    {
        var state = new AppState();
        state.SetPersonSelected(new Person { Name = "A", Email = "a@example.com", Active = true }, true);
        state.SetPersonSelected(new Person { Name = "B", Email = "b@example.com", Active = true }, true);

        state.ClearSelectedPeople();

        Assert.Empty(state.SelectedPeople);
        Assert.False(state.IsPersonSelected("a@example.com"));
        Assert.False(state.IsPersonSelected("b@example.com"));
    }

    [Fact]
    public void SetSelectedSheet_UpdatesCurrentSheet()
    {
        var state = new AppState();
        var sheet = new SheetInfo { Id = "sheet-1", Name = "People" };

        state.SetSelectedSheet(sheet);

        Assert.NotNull(state.SelectedSheet);
        Assert.Equal("sheet-1", state.SelectedSheet!.Id);
        Assert.Equal("People", state.SelectedSheet.Name);
    }
}
