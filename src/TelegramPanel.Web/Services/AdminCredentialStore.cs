using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TelegramPanel.Web.Services;

public static class PanelRoles
{
    public const string Administrator = "admin";
    public const string Operator = "operator";
    public const string Auditor = "auditor";
    public static readonly string[] All = [Administrator, Operator, Auditor];

    public static string Normalize(string? role)
    {
        var value = (role ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "admin" or "administrator" => Administrator,
            "operator" or "operations" => Operator,
            "auditor" or "readonly" or "viewer" => Auditor,
            _ => throw new InvalidOperationException("角色必须为 admin、operator 或 auditor")
        };
    }

    public static IReadOnlyList<string> Permissions(string role) => Normalize(role) switch
    {
        Administrator => ["read", "operate", "admin"],
        Operator => ["read", "operate"],
        _ => ["read"]
    };
}

public sealed record PanelUserProfile(
    string Username,
    string Role,
    bool Enabled,
    bool MustChangePassword,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<string>? NavigationItems);

public sealed record PanelUserIdentity(
    string Username,
    string Role,
    bool MustChangePassword,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string>? NavigationItems);

public sealed class AdminCredentialStore
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IOptionsMonitor<AdminAuthOptions> _options;
    private readonly ILogger<AdminCredentialStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AdminCredentialFile? _cached;

    public AdminCredentialStore(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IOptionsMonitor<AdminAuthOptions> options,
        ILogger<AdminCredentialStore> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _options = options;
        _logger = logger;
    }

    public bool Enabled => _options.CurrentValue.Enabled;
    public string Username => FindPrimaryAdministrator()?.Username
        ?? (_options.CurrentValue.InitialUsername ?? "tgpanel").Trim();
    public bool MustChangePassword => FindPrimaryAdministrator()?.MustChangePassword == true;

    public string CredentialsFilePath => StoragePathResolver.ResolveWritablePath(
        _configuration,
        _environment,
        _options.CurrentValue.CredentialsPath,
        "admin_auth.json");

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cached != null)
                return;

            var path = CredentialsFilePath;
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                var file = JsonSerializer.Deserialize<AdminCredentialFile>(json)
                    ?? throw new InvalidOperationException("admin_auth.json 解析失败");

                if (file.Users.Count == 0 && !string.IsNullOrWhiteSpace(file.Username))
                {
                    file.Users.Add(new PanelUserCredential
                    {
                        Username = file.Username,
                        Role = PanelRoles.Administrator,
                        Enabled = true,
                        SaltBase64 = file.SaltBase64 ?? string.Empty,
                        HashBase64 = file.HashBase64 ?? string.Empty,
                        Iterations = file.Iterations <= 0 ? 150_000 : file.Iterations,
                        MustChangePassword = file.MustChangePassword,
                        CreatedAtUtc = file.CreatedAtUtc,
                        UpdatedAtUtc = file.UpdatedAtUtc
                    });
                    file.Version = 2;
                    ClearLegacyFields(file);
                    await SaveAsync(file, cancellationToken);
                    _logger.LogInformation("后台凭据已从单管理员格式迁移到多用户格式");
                }

                ValidateLoadedFile(file);
                _cached = file;
                return;
            }

            var opt = _options.CurrentValue;
            var initialUsername = (opt.InitialUsername ?? "tgpanel").Trim();
            var initialPassword = (opt.InitialPassword ?? "tgpanel123").Trim();
            if (string.IsNullOrWhiteSpace(initialUsername) || string.IsNullOrWhiteSpace(initialPassword))
                throw new InvalidOperationException("AdminAuth 初始账号/密码未配置");

            var now = DateTime.UtcNow;
            var initialFile = new AdminCredentialFile { Version = 2 };
            initialFile.Users.Add(CreateUserCredential(
                initialUsername,
                initialPassword,
                PanelRoles.Administrator,
                mustChangePassword: true,
                now));

            await SaveAsync(initialFile, cancellationToken);
            _cached = initialFile;
            _logger.LogWarning("后台多用户登录已初始化：账号 {Username}，角色 admin", initialUsername);
        }
        finally
        {
            _lock.Release();
        }
    }

    public PanelUserProfile? GetUserProfile(string? username)
    {
        var user = FindUser(username);
        return user == null ? null : ToProfile(user);
    }

    public bool GetMustChangePassword(string? username) => FindUser(username)?.MustChangePassword == true;

    public async Task<PanelUserIdentity?> AuthenticateAsync(
        string? username,
        string? password,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            return new PanelUserIdentity("admin", PanelRoles.Administrator, false, PanelRoles.Permissions(PanelRoles.Administrator), null);

        await EnsureInitializedAsync(cancellationToken);
        username = (username ?? string.Empty).Trim();
        password = (password ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var user = FindUser(username);
            if (user == null || !user.Enabled || !VerifyPassword(user, password))
                return null;

            return new PanelUserIdentity(
                user.Username,
                PanelRoles.Normalize(user.Role),
                user.MustChangePassword,
                PanelRoles.Permissions(user.Role),
                user.NavigationItems);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ValidateAsync(string? username, string? password, CancellationToken cancellationToken = default) =>
        await AuthenticateAsync(username, password, cancellationToken) != null;

    public async Task<IReadOnlyList<PanelUserProfile>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return (_cached?.Users ?? [])
                .OrderByDescending(user => PanelRoles.Normalize(user.Role) == PanelRoles.Administrator)
                .ThenBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
                .Select(ToProfile)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PanelUserProfile> CreateUserAsync(
        string? username,
        string? password,
        string? role,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        username = NormalizeUsername(username);
        password = NormalizeNewPassword(password);
        role = PanelRoles.Normalize(role);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var file = RequireFile();
            if (file.Users.Any(item => string.Equals(item.Username, username, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("用户名已存在");

            var user = CreateUserCredential(username, password, role, mustChangePassword: true, DateTime.UtcNow);
            file.Users.Add(user);
            await SaveAsync(file, cancellationToken);
            return ToProfile(user);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PanelUserProfile> UpdateUserAsync(
        string actorUsername,
        string targetUsername,
        string? role,
        bool enabled,
        IReadOnlyList<string>? navigationItems,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        role = PanelRoles.Normalize(role);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var file = RequireFile();
            var user = FindUser(targetUsername) ?? throw new InvalidOperationException("用户不存在");
            EnsureAdministratorRemains(file, user, role, enabled);
            if (string.Equals(actorUsername, targetUsername, StringComparison.OrdinalIgnoreCase) && !enabled)
                throw new InvalidOperationException("不能停用当前登录用户");

            user.Role = role;
            user.Enabled = enabled;
            user.NavigationItems = NormalizeNavigationItems(navigationItems);
            user.UpdatedAtUtc = DateTime.UtcNow;
            await SaveAsync(file, cancellationToken);
            return ToProfile(user);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ResetPasswordAsync(string targetUsername, string? newPassword, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var password = NormalizeNewPassword(newPassword);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var file = RequireFile();
            var user = FindUser(targetUsername) ?? throw new InvalidOperationException("用户不存在");
            ApplyPassword(user, password);
            user.MustChangePassword = true;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await SaveAsync(file, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteUserAsync(string actorUsername, string targetUsername, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var file = RequireFile();
            var user = FindUser(targetUsername) ?? throw new InvalidOperationException("用户不存在");
            if (string.Equals(actorUsername, user.Username, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("不能删除当前登录用户");

            EnsureAdministratorRemains(file, user, PanelRoles.Auditor, enabled: false);
            file.Users.Remove(user);
            await SaveAsync(file, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ChangePasswordAsync(
        string username,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            throw new InvalidOperationException("后台验证未启用");

        await EnsureInitializedAsync(cancellationToken);
        currentPassword = (currentPassword ?? string.Empty).Trim();
        newPassword = NormalizeNewPassword(newPassword);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var file = RequireFile();
            var user = FindUser(username) ?? throw new InvalidOperationException("用户不存在");
            if (!VerifyPassword(user, currentPassword))
                throw new InvalidOperationException("当前密码错误");

            ApplyPassword(user, newPassword);
            user.MustChangePassword = false;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await SaveAsync(file, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
        ChangePasswordAsync(Username, currentPassword, newPassword, cancellationToken);

    public async Task ChangeUsernameAsync(
        string username,
        string currentPassword,
        string newUsername,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            throw new InvalidOperationException("后台验证未启用");

        await EnsureInitializedAsync(cancellationToken);
        newUsername = NormalizeUsername(newUsername);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var file = RequireFile();
            var user = FindUser(username) ?? throw new InvalidOperationException("用户不存在");
            if (!VerifyPassword(user, (currentPassword ?? string.Empty).Trim()))
                throw new InvalidOperationException("当前密码错误");
            if (file.Users.Any(item => !ReferenceEquals(item, user)
                && string.Equals(item.Username, newUsername, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("用户名已存在");

            user.Username = newUsername;
            user.MustChangePassword = false;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await SaveAsync(file, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task ChangeUsernameAsync(string currentPassword, string newUsername, CancellationToken cancellationToken = default) =>
        ChangeUsernameAsync(Username, currentPassword, newUsername, cancellationToken);

    private AdminCredentialFile RequireFile() => _cached ?? throw new InvalidOperationException("凭据未初始化");

    private PanelUserCredential? FindPrimaryAdministrator() => _cached?.Users.FirstOrDefault(user =>
        user.Enabled && PanelRoles.Normalize(user.Role) == PanelRoles.Administrator);

    private PanelUserCredential? FindUser(string? username) => _cached?.Users.FirstOrDefault(user =>
        string.Equals(user.Username, (username ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

    private async Task SaveAsync(AdminCredentialFile file, CancellationToken cancellationToken)
    {
        file.Version = 2;
        ClearLegacyFields(file);
        var path = CredentialsFilePath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var temporaryPath = path + ".tmp";
        var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(temporaryPath, json, new System.Text.UTF8Encoding(false), cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
        _cached = file;
    }

    private static PanelUserCredential CreateUserCredential(
        string username,
        string password,
        string role,
        bool mustChangePassword,
        DateTime nowUtc)
    {
        var user = new PanelUserCredential
        {
            Username = NormalizeUsername(username),
            Role = PanelRoles.Normalize(role),
            Enabled = true,
            MustChangePassword = mustChangePassword,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
        ApplyPassword(user, password);
        return user;
    }

    private static void ApplyPassword(PanelUserCredential user, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        const int iterations = 150_000;
        user.SaltBase64 = Convert.ToBase64String(salt);
        user.HashBase64 = Convert.ToBase64String(HashPassword(password, salt, iterations));
        user.Iterations = iterations;
    }

    private static bool VerifyPassword(PanelUserCredential user, string password)
    {
        try
        {
            var salt = Convert.FromBase64String(user.SaltBase64);
            var expected = Convert.FromBase64String(user.HashBase64);
            var actual = HashPassword(password, salt, user.Iterations);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] HashPassword(string password, byte[] salt, int iterations)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32);
    }

    private static PanelUserProfile ToProfile(PanelUserCredential user) => new(
        user.Username,
        PanelRoles.Normalize(user.Role),
        user.Enabled,
        user.MustChangePassword,
        user.CreatedAtUtc,
        user.UpdatedAtUtc,
        user.NavigationItems);

    private static List<string>? NormalizeNavigationItems(IReadOnlyList<string>? items)
    {
        if (items == null) return null;
        return items.Select(x => (x ?? string.Empty).Trim())
            .Where(x => x.StartsWith('/') || x.EndsWith("-group", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void EnsureAdministratorRemains(AdminCredentialFile file, PanelUserCredential target, string nextRole, bool enabled)
    {
        if (!target.Enabled || PanelRoles.Normalize(target.Role) != PanelRoles.Administrator)
            return;
        if (enabled && PanelRoles.Normalize(nextRole) == PanelRoles.Administrator)
            return;

        var otherAdministrators = file.Users.Count(user =>
            !ReferenceEquals(user, target) && user.Enabled
            && PanelRoles.Normalize(user.Role) == PanelRoles.Administrator);
        if (otherAdministrators == 0)
            throw new InvalidOperationException("系统至少需要保留一个启用的管理员");
    }

    private static void ValidateLoadedFile(AdminCredentialFile file)
    {
        if (file.Users.Count == 0)
            throw new InvalidOperationException("后台用户文件中没有用户");
        if (file.Users.GroupBy(user => user.Username, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidOperationException("后台用户文件中存在重复用户名");
        if (!file.Users.Any(user => user.Enabled && PanelRoles.Normalize(user.Role) == PanelRoles.Administrator))
            throw new InvalidOperationException("后台用户文件中没有启用的管理员");
        foreach (var user in file.Users)
        {
            user.Username = NormalizeUsername(user.Username);
            user.Role = PanelRoles.Normalize(user.Role);
        }
    }

    private static void ClearLegacyFields(AdminCredentialFile file)
    {
        file.Username = null;
        file.SaltBase64 = null;
        file.HashBase64 = null;
        file.Iterations = 0;
        file.MustChangePassword = false;
        file.CreatedAtUtc = default;
        file.UpdatedAtUtc = default;
    }

    internal static bool TryNormalizeUsername(string? username, out string normalizedUsername, out string? error)
    {
        normalizedUsername = (username ?? string.Empty).Trim();
        if (normalizedUsername.Length < 4 || normalizedUsername.Length > 32)
        {
            error = "后台用户名长度应为 4-32 位";
            return false;
        }
        if (!normalizedUsername.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.'))
        {
            error = "后台用户名只能包含字母、数字、下划线、短横线或点";
            return false;
        }
        if (string.Equals(normalizedUsername, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedUsername, "administrator", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedUsername, "root", StringComparison.OrdinalIgnoreCase))
        {
            error = "请不要使用常见后台用户名";
            return false;
        }
        error = null;
        return true;
    }

    private static string NormalizeUsername(string? username)
    {
        if (!TryNormalizeUsername(username, out var normalizedUsername, out var error))
            throw new InvalidOperationException(error);
        return normalizedUsername;
    }

    private static string NormalizeNewPassword(string? password)
    {
        var normalized = (password ?? string.Empty).Trim();
        if (normalized.Length < 6)
            throw new InvalidOperationException("新密码长度至少 6 位");
        return normalized;
    }

    private sealed class AdminCredentialFile
    {
        public int Version { get; set; } = 2;
        public List<PanelUserCredential> Users { get; set; } = [];
        public string? Username { get; set; }
        public string? SaltBase64 { get; set; }
        public string? HashBase64 { get; set; }
        public int Iterations { get; set; }
        public bool MustChangePassword { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    private sealed class PanelUserCredential
    {
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = PanelRoles.Auditor;
        public bool Enabled { get; set; } = true;
        public string SaltBase64 { get; set; } = string.Empty;
        public string HashBase64 { get; set; } = string.Empty;
        public int Iterations { get; set; } = 150_000;
        public bool MustChangePassword { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public List<string>? NavigationItems { get; set; }
    }
}
