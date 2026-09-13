using TelegramPanel.Web.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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

    [Fact]
    public async Task Auditor_CanChangeOwnInitialPassword()
    {
        var called = false;
        var context = CreateFilterContext("POST", "/api/panel/settings/password", PanelRoles.Auditor);

        await PanelPermissionGuard.InvokeAsync(context, _ =>
        {
            called = true;
            return ValueTask.FromResult<object?>(new object());
        });

        Assert.True(called);
    }

    [Fact]
    public async Task Auditor_RemainsReadOnlyForOtherPostRequests()
    {
        var called = false;
        var context = CreateFilterContext("POST", "/api/panel/channels", PanelRoles.Auditor);

        await PanelPermissionGuard.InvokeAsync(context, _ =>
        {
            called = true;
            return ValueTask.FromResult<object?>(new object());
        });

        Assert.False(called);
    }

    private static EndpointFilterInvocationContext CreateFilterContext(string method, string path, string role)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.Request.Path = path;
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "test"),
            new Claim(ClaimTypes.Role, role)
        ], "test"));
        return EndpointFilterInvocationContext.Create(http);
    }
}
