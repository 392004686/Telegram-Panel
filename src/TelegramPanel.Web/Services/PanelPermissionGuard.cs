using System.Security.Claims;

namespace TelegramPanel.Web.Services;

public static class PanelPermissionGuard
{
    private static readonly string[] AdministratorOnlyPrefixes =
    [
        "/api/panel/users",
        "/api/panel/modules",
        "/api/panel/external-apis",
        "/api/panel/system"
    ];

    private static readonly string[] AdministratorOnlySettingsPrefixes =
    [
        "/api/panel/settings/telegram-api",
        "/api/panel/settings/global-proxy",
        "/api/panel/settings/cloud-mail",
        "/api/panel/settings/ai",
        "/api/panel/settings/batch",
        "/api/panel/settings/time-zone",
        "/api/panel/settings/logging",
        "/api/panel/settings/bucket-backup",
        "/api/panel/settings/cache"
    ];

    public static async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var role = NormalizeClaimRole(http.User.FindFirstValue(ClaimTypes.Role));
        var method = http.Request.Method;
        var path = http.Request.Path.Value ?? string.Empty;

        // When AdminAuth is disabled there is no authenticated principal; the panel
        // keeps its historical unrestricted behavior. With AdminAuth enabled the
        // authorization middleware rejects anonymous requests before this filter.
        if (http.User.Identity?.IsAuthenticated != true)
            return await next(context);

        if (role == PanelRoles.Administrator)
            return await next(context);

        if (role == PanelRoles.Auditor
            && !HttpMethods.IsGet(method)
            && !HttpMethods.IsHead(method)
            && !IsAuditorCredentialSelfService(path, method))
            return Forbidden("只读审计员仅可查看数据");

        if (role == PanelRoles.Operator && IsAdministratorOnly(path, method))
            return Forbidden("当前操作需要管理员权限");

        return await next(context);
    }

    private static bool IsAdministratorOnly(string path, string method)
    {
        if (AdministratorOnlyPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return true;
        if (AdministratorOnlySettingsPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return true;
        if (path.StartsWith("/api/panel/version-info", StringComparison.OrdinalIgnoreCase)
            && !HttpMethods.IsGet(method)
            && !HttpMethods.IsHead(method))
            return true;
        if (HttpMethods.IsDelete(method))
            return true;
        if (path.Contains("/delete", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/cleanup", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static bool IsAuditorCredentialSelfService(string path, string method) =>
        HttpMethods.IsPost(method)
        && path.Equals("/api/panel/settings/password", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeClaimRole(string? role)
    {
        try
        {
            return PanelRoles.Normalize(role);
        }
        catch
        {
            return PanelRoles.Auditor;
        }
    }

    private static IResult Forbidden(string message) => Results.Json(
        new { success = false, message, code = "PERMISSION_DENIED" },
        statusCode: StatusCodes.Status403Forbidden);
}
