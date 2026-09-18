using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
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
    private const int InternalMultiplier = 3;
    private sealed record DeviceSpec(int Width, int Height, string Platform, float StatusFraction, string? StatusBarFile);

    private static readonly Dictionary<string, DeviceSpec> Devices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["iphone-16-pro-max"] = new(440, 956, "ios", 0.065812f, "iPhone 16 Pro and 16 Max Status Bar Black.png"),
        ["iphone-16-pro"] = new(402, 874, "ios", 0.073892f, "iPhone 16 Pro and 16 Max Status Bar Black.png"),
        ["iphone-16-plus"] = new(430, 932, "ios", 0.064039f, "iPhone 16 and 16 Plus Status Bar Black.png"),
        ["iphone-16"] = new(393, 852, "ios", 0.068966f, "iPhone 16 and 16 Plus Status Bar Black.png"),
        ["iphone-15-pro-max"] = new(430, 932, "ios", 0.065f, "iPhone 16 Pro and 16 Max Status Bar Black.png"),
        ["iphone-15-pro"] = new(393, 852, "ios", 0.065f, "iPhone 16 Pro and 16 Max Status Bar Black.png"),
        ["iphone-14-pro-max"] = new(430, 932, "ios", 0.065f, "iPhone 16 Pro and 16 Max Status Bar Black.png"),
        ["iphone-14-pro"] = new(393, 852, "ios", 0.065f, "iPhone 16 Pro and 16 Max Status Bar Black.png"),
        ["iphone-14"] = new(390, 844, "ios", 0.055f, "Notch Status Bar Black.png"),
        ["iphone-13"] = new(390, 844, "ios", 0.055f, "Notch Status Bar Black.png"),
        ["pixel-7-pro"] = new(412, 816, "android", 48f / 816f, null),
        ["pixel-8-pro"] = new(448, 921, "android", 48f / 921f, null),
        ["pixel-9-pro-xl"] = new(448, 921, "android", 48f / 921f, null),
        ["galaxy-s24"] = new(360, 780, "android", 48f / 780f, null),
        ["galaxy-a55"] = new(480, 1040, "android", 48f / 1040f, null),
        ["galaxy-z-flip-6"] = new(360, 804, "android", 48f / 804f, null),
    };

    private static readonly object FontGate = new();
    private static FontFamily? StatusFontFamily;

    public async Task<byte[]> ComposeAsync(Stream baseImage, MaterialMockupRequest request, CancellationToken cancellationToken = default)
    {
        if (!Devices.TryGetValue((request.DeviceId ?? string.Empty).Trim(), out var device))
            device = Devices["iphone-16-pro-max"];

        var scale = request.Scale <= 0 ? 1 : Math.Clamp(request.Scale, 0.1, 4);
        var screenW = device.Width * InternalMultiplier;
        var screenH = device.Height * InternalMultiplier;
        var statusH = Math.Max(24, (int)Math.Round(screenH * device.StatusFraction));
        var homeH = Math.Max(12, (int)Math.Round(screenH * 0.025));
        var contentH = Math.Max(80, screenH - statusH - homeH);
        var clock = ResolveTime(request);

        using var source = await Image.LoadAsync<Rgba32>(baseImage, cancellationToken);
        using var content = PrepareContent(source, screenW, contentH);
        using var status = await CreateStatusBarAsync(device, screenW, statusH, clock, request.Notification, cancellationToken);
        using var home = CreateHomeIndicator(screenW, screenH, homeH);
        using var canvas = new Image<Rgba32>(screenW, screenH, Color.White);
        canvas.Mutate(x =>
        {
            x.DrawImage(status, new Point(0, 0), 1f);
            x.DrawImage(content, new Point(0, statusH), 1f);
            x.DrawImage(home, new Point(0, statusH + contentH), 1f);
        });

        var outW = Math.Max(1, (int)Math.Round(device.Width * scale));
        var outH = Math.Max(1, (int)Math.Round(device.Height * scale));
        if (outW != screenW || outH != screenH)
            canvas.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(outW, outH), Mode = ResizeMode.Stretch, Sampler = KnownResamplers.Lanczos3 }));

        await using var ms = new MemoryStream();
        await canvas.SaveAsPngAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private static Image<Rgba32> PrepareContent(Image<Rgba32> source, int width, int height)
    {
        source.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(width, 0),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));
        if (source.Width != width)
            source.Mutate(x => x.Resize(width, Math.Max(1, (int)Math.Round(source.Height * (width / (double)Math.Max(1, source.Width))))));

        var canvas = new Image<Rgba32>(width, height, Color.White);
        if (source.Height <= height)
        {
            canvas.Mutate(x => x.DrawImage(source, new Point(0, 0), 1f));
            return canvas;
        }

        var stickyH = Math.Max(1, Math.Min(height, (int)Math.Round(80d * width / Math.Max(1, source.Width))));
        var topH = Math.Max(1, height - stickyH);
        using var top = source.Clone(ctx => ctx.Crop(new Rectangle(0, 0, width, Math.Min(topH, source.Height))));
        var stickyTop = Math.Max(0, source.Height - stickyH);
        using var sticky = source.Clone(ctx => ctx.Crop(new Rectangle(0, stickyTop, width, Math.Min(stickyH, source.Height - stickyTop))));
        canvas.Mutate(x =>
        {
            x.DrawImage(top, new Point(0, 0), 1f);
            x.DrawImage(sticky, new Point(0, topH), 1f);
        });
        return canvas;
    }

    private static async Task<Image<Rgba32>> CreateStatusBarAsync(DeviceSpec device, int width, int height, string clock, string notification, CancellationToken cancellationToken)
    {
        if (device.Platform == "android")
            return CreateAndroidStatusBar(width, height, clock, notification);

        Image<Rgba32> bar;
        var asset = ResolveStatusBarPath(device.StatusBarFile);
        if (!string.IsNullOrWhiteSpace(asset) && File.Exists(asset))
        {
            await using var fs = File.OpenRead(asset);
            using var loaded = await Image.LoadAsync<Rgba32>(fs, cancellationToken);
            bar = loaded.Clone(ctx => ctx.Resize(new ResizeOptions { Size = new Size(width, height), Mode = ResizeMode.Stretch, Sampler = KnownResamplers.Lanczos3 }));
        }
        else
        {
            bar = new Image<Rgba32>(width, height, Color.White);
        }

        var coverW = Math.Max(1, (int)Math.Round(width * 0.29));
        bar.Mutate(x => x.Fill(Color.White, new RectangularPolygon(0, 0, coverW, height)));
        // 与购物网站截图素材项目 officialStatusBar() 的 SVG 基线完全一致。
        // SVG 的 y 是文字基线；ImageSharp 使用 Bottom 才能复现该坐标，Center
        // 会把文字整体下移并放大视觉占比，造成时间与官方状态栏错位。
        DrawTime(bar, clock, width * 0.105f, height * 0.68f, Math.Max(10, height * 0.43f), HorizontalAlignment.Center, VerticalAlignment.Bottom);
        return bar;
    }

    private static Image<Rgba32> CreateAndroidStatusBar(int width, int height, string clock, string notification)
    {
        var img = new Image<Rgba32>(width, height, Color.White);
        var s = width / 412f;
        var ink = new Color(new Rgba32(17, 17, 17));
        DrawTime(img, clock, 16 * s, 30 * s, Math.Max(10, 15 * s), HorizontalAlignment.Left, VerticalAlignment.Bottom);
        img.Mutate(x =>
        {
            x.Fill(ink, new EllipsePolygon(width / 2f, 13 * s, Math.Max(2f, 5 * s)));
            if (string.Equals(notification, "telegram", StringComparison.OrdinalIgnoreCase))
            {
                var px = 58 * s;
                var py = 15 * s;
                var plane = new PathBuilder();
                plane.AddLines(new PointF(px, py + 16 * s), new PointF(px + 22 * s, py + 8 * s), new PointF(px, py));
                x.Fill(ink, plane.Build());
            }

            var ox = width - 106 * s;
            var oy = 12 * s;
            for (var i = 0; i < 4; i++)
            {
                var h = (4 + i * 3) * s;
                x.Fill(ink, new RectangularPolygon(ox + i * 6 * s, oy + 16 * s - h, Math.Max(2f, 3 * s), h));
            }

            x.Draw(ink, Math.Max(1f, 1.5f * s), new RectangularPolygon(ox + 70 * s, oy, 29 * s, 17 * s));
            x.Fill(ink, new RectangularPolygon(ox + 101 * s, oy + 5 * s, 2 * s, 7 * s));
        });
        DrawTime(img, "45", width - 106 * s + 84.5f * s, oyText(s), Math.Max(8, 9 * s), HorizontalAlignment.Center, VerticalAlignment.Bottom);
        return img;
    }

    private static float oyText(float s) => 12 * s + 12.5f * s;

    private static Image<Rgba32> CreateHomeIndicator(int width, int screenH, int height)
    {
        var img = new Image<Rgba32>(width, height, Color.White);
        var indicatorW = Math.Max(40, (int)Math.Round(width * 0.285));
        var indicatorH = Math.Max(3, (int)Math.Round(screenH * 0.0045));
        var x = (width - indicatorW) / 2f;
        var y = height * 0.45f;
        img.Mutate(ctx => ctx.Fill(Color.Black, new RectangularPolygon(x, y, indicatorW, indicatorH)));
        return img;
    }

    private static void DrawTime(Image<Rgba32> image, string text, float x, float y, float fontSize, HorizontalAlignment hAlign, VerticalAlignment vAlign)
    {
        var font = GetStatusFont(fontSize);
        var options = new RichTextOptions(font)
        {
            Origin = new PointF(x, y),
            HorizontalAlignment = hAlign,
            VerticalAlignment = vAlign,
            Dpi = 96
        };
        image.Mutate(ctx => ctx.DrawText(options, text, Color.Black));
    }

    private static Font GetStatusFont(float size)
    {
        lock (FontGate)
        {
            if (StatusFontFamily == null)
            {
                var fontPath = ResolveAssetPath(System.IO.Path.Combine("fonts", "StatusBarBold.ttf"));
                if (!string.IsNullOrWhiteSpace(fontPath) && File.Exists(fontPath))
                {
                    var collection = new FontCollection();
                    StatusFontFamily = collection.Add(fontPath);
                }
                else if (SystemFonts.TryGet("Arial", out var arial))
                {
                    StatusFontFamily = arial;
                }
                else if (SystemFonts.TryGet("DejaVu Sans", out var dejavu))
                {
                    StatusFontFamily = dejavu;
                }
                else
                {
                    StatusFontFamily = SystemFonts.Families.First();
                }
            }

            return StatusFontFamily.Value.CreateFont(size, FontStyle.Bold);
        }
    }

    private static string ResolveTime(MaterialMockupRequest request)
    {
        if (string.Equals(request.TimeMode, "fixed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(request.Time))
        {
            var text = request.Time.Trim();
            if (TimeSpan.TryParse(text, out var span))
                return string.Format("{0:00}:{1:00}", (int)span.TotalHours % 24, span.Minutes);
            return text;
        }

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"); }
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).ToString("HH:mm");
    }

    private static string? ResolveStatusBarPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        return ResolveAssetPath(System.IO.Path.Combine("mockify", "status-bar", fileName));
    }

    internal static string? ResolveAssetPath(string relative)
    {
        foreach (var root in EnumerateAssetRoots())
        {
            var candidate = System.IO.Path.Combine(root, relative);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static IEnumerable<string> EnumerateAssetRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                var direct = System.IO.Path.Combine(dir.FullName, "Assets");
                if (seen.Add(direct)) yield return direct;
                var web = System.IO.Path.Combine(dir.FullName, "src", "TelegramPanel.Web", "Assets");
                if (seen.Add(web)) yield return web;
                dir = dir.Parent;
            }
        }
    }
}
