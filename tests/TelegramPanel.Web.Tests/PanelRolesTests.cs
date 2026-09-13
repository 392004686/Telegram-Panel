using TelegramPanel.Web.Services;
using Xunit;

namespace TelegramPanel.Web.Tests;

public sealed class PanelRolesTests
{
    [Theory]
    [InlineData("admin", "admin")]
    [InlineData("Administrator", "admin")]
    [InlineData("operations", "operator")]
    [InlineData("viewer", "auditor")]
    public void Normalize_AcceptsCanonicalNamesAndAliases(string input, string expected)
    {
        Assert.Equal(expected, PanelRoles.Normalize(input));
    }

    [Fact]
    public void Permissions_AreStrictlyNestedByRole()
    {
        Assert.Equal(new[] { "read", "operate", "admin" }, PanelRoles.Permissions(PanelRoles.Administrator));
        Assert.Equal(new[] { "read", "operate" }, PanelRoles.Permissions(PanelRoles.Operator));
        Assert.Equal(new[] { "read" }, PanelRoles.Permissions(PanelRoles.Auditor));
    }

    [Fact]
    public void Normalize_RejectsUnknownRole()
    {
        Assert.Throws<InvalidOperationException>(() => PanelRoles.Normalize("owner"));
    }
}
