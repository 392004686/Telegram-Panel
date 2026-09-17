using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TelegramPanel.Web.Services;

public sealed class MaterialMockupRequest
{
    public string DeviceId { get; set; } = "iphone-16-pro-max";
    public string TimeMode { get; set; } = "now";
    public string? Time { get; set; }
    public double Scale { get; set; } = 1;
    public string Notification { get; set; } = "none";
}

public sealed class MaterialMockupService
{
    private sealed record DeviceSpec(int Width, int Height, string Platform, float StatusFraction);
    private static readonly Dictionary<string, DeviceSpec> Devices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["iphone-16-pro-max"] = new(440, 956, "ios", 0.054f),
        ["iphone-16-pro"] = new(402, 874, "ios", 0.054f),
        ["iphone-16-plus"] = new(430, 932, "ios", 0.054f),
        ["iphone-16"] = new(393, 852, "ios", 0.054f),
        ["iphone-15-pro-max"] = new(430, 932, "ios", 0.054f),
        ["iphone-14-pro-max"] = new(430, 932, "ios", 0.054f),
        ["pixel-7-pro"] = new(412, 816, "android", 48f / 816f),
        ["pixel-8-pro"] = new(448, 921, "android", 48f / 921f),
        ["galaxy-s24"] = new(360, 780, "android", 48f / 780f),
        ["galaxy-a55"] = new(480, 1040, "android", 48f / 1040f),
    };

    public async Task<byte[]> ComposeAsync(Stream baseImage, MaterialMockupRequest request, CancellationToken cancellationToken = default)
    {
        if (!Devices.TryGetValue((request.DeviceId ?? string.Empty).Trim(), out var device))
            device = Devices["iphone-16-pro-max"];
        var scale = request.Scale <= 0 ? 1 : Math.Clamp(request.Scale, 0.5, 2);
        var screenW = Math.Max(220, (int)Math.Round(device.Width * scale));
        var screenH = Math.Max(400, (int)Math.Round(device.Height * scale));
        var statusH = Math.Max(28, (int)Math.Round(screenH * device.StatusFraction));
        var homeH = Math.Max(12, (int)Math.Round(screenH * 0.025));
        var contentH = Math.Max(80, screenH - statusH - homeH);
        var clock = ResolveTime(request);
        using var source = await Image.LoadAsync<Rgba32>(baseImage, cancellationToken);
        source.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(screenW, contentH), Mode = ResizeMode.Crop, Position = AnchorPositionMode.Top }));
        using var canvas = new Image<Rgba32>(screenW, screenH, Color.White);
        DrawStatusBar(canvas, screenW, statusH, clock, device.Platform, request.Notification);
        canvas.Mutate(x => x.DrawImage(source, new Point(0, statusH), 1f));
        DrawHomeIndicator(canvas, screenW, statusH + contentH, homeH);
        await using var ms = new MemoryStream();
        await canvas.SaveAsPngAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private static string ResolveTime(MaterialMockupRequest request)
    {
        if (string.Equals(request.TimeMode, "fixed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(request.Time))
            return request.Time.Trim();
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"); }
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).ToString("HH:mm");
    }

    private static void DrawStatusBar(Image<Rgba32> canvas, int width, int height, string clock, string platform, string notification)
    {
        FillRect(canvas, 0, 0, width, height, new Rgba32(255, 255, 255));
        var unit = Math.Max(1, height / 14);
        DrawText(canvas, 12, Math.Max(6, height / 2 - unit * 4), clock, new Rgba32(17, 17, 17), unit);
        if (platform == "ios")
        {
            var pillW = Math.Max(48, width / 5);
            var pillH = Math.Max(12, height / 2);
            FillRect(canvas, (width - pillW) / 2, Math.Max(4, height / 6), pillW, pillH, new Rgba32(5, 5, 5));
        }
        else if (string.Equals(notification, "telegram", StringComparison.OrdinalIgnoreCase))
        {
            DrawTelegramMark(canvas, 12 + clock.Length * unit * 6, Math.Max(6, height / 3), unit * 2);
        }
        DrawSignal(canvas, width - height * 3, height / 3, unit);
    }

    private static void DrawHomeIndicator(Image<Rgba32> canvas, int width, int top, int height)
    {
        FillRect(canvas, 0, top, width, height, new Rgba32(255, 255, 255));
        var barW = Math.Max(40, width / 4);
        var barH = Math.Max(3, height / 5);
        FillRect(canvas, (width - barW) / 2, top + height / 2, barW, barH, new Rgba32(0, 0, 0));
    }

    private static void DrawSignal(Image<Rgba32> canvas, int x, int y, int unit)
    {
        for (var i = 0; i < 4; i++)
        {
            var h = (i + 1) * unit;
            FillRect(canvas, x + i * (unit + 1), y + 4 * unit - h, unit, h, new Rgba32(17, 17, 17));
        }
        FillRect(canvas, x + 6 * unit, y + unit, unit * 3, unit * 2, new Rgba32(17, 17, 17));
    }

    private static void DrawTelegramMark(Image<Rgba32> canvas, int x, int y, int size)
    {
        for (var i = 0; i < size; i++)
            FillRect(canvas, x + i, y + size / 2 - i / 3, Math.Max(1, size - i), Math.Max(1, i / 2 + 1), new Rgba32(17, 17, 17));
    }

    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['0'] = new[] { "111", "101", "101", "101", "111" },
        ['1'] = new[] { "010", "110", "010", "010", "111" },
        ['2'] = new[] { "111", "001", "111", "100", "111" },
        ['3'] = new[] { "111", "001", "111", "001", "111" },
        ['4'] = new[] { "101", "101", "111", "001", "001" },
        ['5'] = new[] { "111", "100", "111", "001", "111" },
        ['6'] = new[] { "111", "100", "111", "101", "111" },
        ['7'] = new[] { "111", "001", "001", "001", "001" },
        ['8'] = new[] { "111", "101", "111", "101", "111" },
        ['9'] = new[] { "111", "101", "111", "001", "111" },
        [':'] = new[] { "0", "1", "0", "1", "0" },
    };

    private static void DrawText(Image<Rgba32> canvas, int x, int y, string text, Rgba32 color, int unit)
    {
        var cx = x;
        foreach (var ch in text)
        {
            if (!Glyphs.TryGetValue(ch, out var rows)) { cx += unit * 3; continue; }
            for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] == '1')
                    FillRect(canvas, cx + c * unit, y + r * unit, unit, unit, color);
            cx += (rows[0].Length + 1) * unit;
        }
    }

    private static void FillRect(Image<Rgba32> canvas, int x, int y, int w, int h, Rgba32 color)
    {
        var x2 = Math.Min(canvas.Width, x + w);
        var y2 = Math.Min(canvas.Height, y + h);
        x = Math.Max(0, x); y = Math.Max(0, y);
        for (var yy = y; yy < y2; yy++)
        for (var xx = x; xx < x2; xx++)
            canvas[xx, yy] = color;
    }
}
