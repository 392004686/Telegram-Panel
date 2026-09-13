using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace TelegramPanel.Core.Services.Telegram;

public sealed record TelegramDeviceProfileDefinition(
    string Key,
    string Name,
    string Family,
    string AppVersion,
    string DeviceModel,
    string SystemVersion,
    string SystemLangCode = "en-US",
    string LangCode = "en",
    bool Enabled = true,
    bool BuiltIn = false,
    string? Notes = null)
{
    public TelegramClientDeviceProfile ToClientProfile() => new(
        AppVersion,
        DeviceModel,
        SystemVersion,
        SystemLangCode,
        LangCode);
}

public static class TelegramDeviceProfileCatalog
{
    public const string DefaultProfileKey = "android-default";
    public const string RandomProfileKey = "random";
    public const string ImportedProfileKey = "imported-json";
    private const string ImportedProfilePrefix = "imported-json:";

    private static readonly TelegramDeviceProfileDefinition[] BuiltInProfiles =
    {
        new("android-default", "Android 默认指纹", "android", "12.7.3", "Samsung SM-G991B", "Android 14", BuiltIn: true, Notes: "适合统一使用安卓设备画像的账号。"),
        new("ios-default", "iOS 默认指纹", "ios", "10.15.0", "iPhone 15", "iOS 17.5", BuiltIn: true, Notes: "iOS 设备画像；不会改变当前 API 配置。"),
        new("macos-default", "macOS 默认指纹", "macos", "10.15.4", "MacBook Pro", "macOS 14.6", BuiltIn: true),
        new("windows-default", "Windows 默认指纹", "windows", "5.16.4 x64", "PC 64bit", "Windows 11", BuiltIn: true),
    };

    public static IReadOnlyList<TelegramDeviceProfileDefinition> ReadProfiles(IConfiguration configuration)
    {
        var configured = new List<TelegramDeviceProfileDefinition>();
        foreach (var child in configuration.GetSection("Telegram:DeviceProfiles").GetChildren())
        {
            var key = NormalizeKey(child["Key"]);
            if (string.IsNullOrWhiteSpace(key) || IsRandomProfileKey(key))
                continue;

            var fallback = BuiltInProfiles.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            configured.Add(new TelegramDeviceProfileDefinition(
                key,
                NormalizeText(child["Name"], fallback?.Name ?? key),
                NormalizeText(child["Family"], fallback?.Family ?? "custom"),
                NormalizeText(child["AppVersion"], fallback?.AppVersion ?? "5.16.4"),
                NormalizeText(child["DeviceModel"], fallback?.DeviceModel ?? "PC 64bit"),
                NormalizeText(child["SystemVersion"], fallback?.SystemVersion ?? "Windows 11"),
                NormalizeText(child["SystemLangCode"], fallback?.SystemLangCode ?? "en-US"),
                NormalizeText(child["LangCode"], fallback?.LangCode ?? "en"),
                !bool.TryParse(child["Enabled"], out var enabled) || enabled,
                fallback?.BuiltIn == true,
                NormalizeNullable(child["Notes"])));
        }

        foreach (var builtIn in BuiltInProfiles)
        {
            if (configured.All(x => !string.Equals(x.Key, builtIn.Key, StringComparison.OrdinalIgnoreCase)))
                configured.Insert(0, builtIn);
        }

        return configured
            .Where(x => x.Enabled)
            .OrderBy(x => x.BuiltIn ? 0 : 1)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ResolveDefaultKey(IConfiguration configuration)
    {
        var requested = NormalizeKey(configuration["Telegram:DefaultDeviceProfileKey"]);
        if (string.IsNullOrWhiteSpace(requested))
            return DefaultProfileKey;
        return IsRandomProfileKey(requested) ? RandomProfileKey : requested;
    }

    public static TelegramDeviceProfileDefinition? Find(IConfiguration configuration, string? key)
    {
        var normalized = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = ResolveDefaultKey(configuration);
        if (IsRandomProfileKey(normalized))
            return null;
        return ReadProfiles(configuration).FirstOrDefault(x => string.Equals(x.Key, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static TelegramClientDeviceProfile ResolveClientProfile(
        IConfiguration configuration,
        int apiId,
        string? profileKey,
        string stableKey)
    {
        if (TryDecodeImportedProfile(profileKey, out var imported))
            return imported;
        var definition = Find(configuration, profileKey);
        return definition?.ToClientProfile() ?? TelegramClientDeviceProfile.ForStableKey(apiId, stableKey);
    }

    public static string NormalizeKey(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsRandomProfileKey(string? value) =>
        string.Equals(NormalizeKey(value), RandomProfileKey, StringComparison.Ordinal);

    public static string BuildImportedProfileKey(JsonElement root, int apiId, string stableKey)
    {
        static string? Read(JsonElement element, params string[] names)
        {
            foreach (var name in names)
                if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString()!.Trim();
            return null;
        }
        var fallback = TelegramClientDeviceProfile.ForStableKey(apiId, stableKey);
        var values = new[]
        {
            Read(root, "app_version", "appVersion") ?? fallback.AppVersion,
            Read(root, "device_model", "deviceModel") ?? fallback.DeviceModel,
            Read(root, "system_version", "systemVersion") ?? fallback.SystemVersion,
            Read(root, "system_lang_code", "systemLangCode") ?? fallback.SystemLangCode,
            Read(root, "lang_code", "langCode") ?? fallback.LangCode
        };
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return ImportedProfilePrefix + payload;
    }

    private static bool TryDecodeImportedProfile(string? key, out TelegramClientDeviceProfile profile)
    {
        profile = default!;
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(ImportedProfilePrefix, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var payload = key[ImportedProfilePrefix.Length..].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            var values = JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            if (values is not { Length: 5 } || values.Any(string.IsNullOrWhiteSpace)) return false;
            profile = new TelegramClientDeviceProfile(values[0], values[1], values[2], values[3], values[4]);
            return true;
        }
        catch { return false; }
    }

    public static bool TryNormalizeSelectableKey(IConfiguration configuration, string? key, out string? normalizedKey)
    {
        var requested = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(requested))
        {
            normalizedKey = null;
            return true;
        }

        if (IsRandomProfileKey(requested))
        {
            normalizedKey = RandomProfileKey;
            return true;
        }

        if (requested == ImportedProfileKey || TryDecodeImportedProfile(requested, out _))
        {
            normalizedKey = requested;
            return true;
        }

        var profile = Find(configuration, requested);
        if (profile == null)
        {
            normalizedKey = null;
            return false;
        }

        normalizedKey = profile.Key;
        return true;
    }

    private static string NormalizeText(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
