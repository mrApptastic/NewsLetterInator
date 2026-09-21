using NewsLetterInator.App.Services;
using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.Tests;

public class GmailServiceTemplateTests
{
    [Fact]
    public void RenderTemplate_ReplacesAllKnownFieldsIncludingCustomField10()
    {
        var person = new Person
        {
            Name = "Henri",
            Email = "henri@example.com",
            CustomField_1 = "One",
            CustomField_2 = "Two",
            CustomField_3 = "Three",
            CustomField_4 = "Four",
            CustomField_5 = "Five",
            CustomField_6 = "Six",
            CustomField_7 = "Seven",
            CustomField_8 = "Eight",
            CustomField_9 = "Nine",
            CustomField_10 = "Ten"
        };

        const string template = "Hi {Name} ({Email}) | {CustomField_1} {CustomField_2} {CustomField_3} {CustomField_4} {CustomField_5} {CustomField_6} {CustomField_7} {CustomField_8} {CustomField_9} {CustomField_10}";

        var rendered = GmailService.RenderTemplate(template, person);

        Assert.Equal("Hi Henri (henri@example.com) | One Two Three Four Five Six Seven Eight Nine Ten", rendered);
    }

    [Fact]
    public void RenderTemplate_IsCaseInsensitiveForTokens()
    {
        var person = new Person
        {
            Name = "Alex",
            Email = "alex@example.com",
            CustomField_10 = "Value10"
        };

        const string template = "{name} {EMAIL} {customfield_10}";

        var rendered = GmailService.RenderTemplate(template, person);

        Assert.Equal("Alex alex@example.com Value10", rendered);
    }

    [Fact]
    public void RenderTemplate_WhenTemplateIsNull_ReturnsEmptyString()
    {
        var person = new Person { Name = "Alex", Email = "alex@example.com" };

        var rendered = GmailService.RenderTemplate(null!, person);

        Assert.Equal(string.Empty, rendered);
    }
}
