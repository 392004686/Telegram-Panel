using TelegramPanel.Core.Services.Telegram;

namespace TelegramPanel.Web.Services;

public sealed class EngagementRuleSendResult
{
    public int TextCount { get; init; }
    public int ImageCount { get; init; }
    public int MaterialCount { get; init; }
    public bool GeneratedMaterial { get; init; }
}

public sealed class EngagementMessageDispatchService
{
    private readonly TemplateRenderingService _templates;
    private readonly ImageAssetStorageService _assets;
    private readonly MaterialMockupService _mockup;
    private readonly AccountTelegramToolsService _tools;

    public EngagementMessageDispatchService(
        TemplateRenderingService templates,
        ImageAssetStorageService assets,
        MaterialMockupService mockup,
        AccountTelegramToolsService tools)
    {
        _templates = templates;
        _assets = assets;
        _mockup = mockup;
        _tools = tools;
    }

    public async Task<EngagementRuleSendResult> SendRuleAsync(
        int accountId,
        ResolvedChatTarget target,
        CustomerGroupEngagementTaskHandler.MessageRule rule,
        string logLabel,
        Func<string, Task>? writeLog,
        CancellationToken cancellationToken)
    {
        var textCount = 0;
        var imageCount = 0;
        var materialCount = 0;
        var generated = false;

        async Task sendOne(CustomerGroupEngagementTaskHandler.MessageRule item)
        {
            var text = string.IsNullOrWhiteSpace(item.Text) ? string.Empty : await _templates.RenderTextTemplateAsync(item.Text, cancellationToken);
            var materialToken = item.MaterialDictionaryToken;
            var imageToken = item.ImageDictionaryToken;
            if (!string.IsNullOrWhiteSpace(materialToken))
            {
                var asset = await _templates.ResolveImageTemplateAsync(materialToken, cancellationToken);
                await using var baseImage = await _assets.OpenReadAsync(asset.AssetPath, cancellationToken);
                var png = await _mockup.ComposeAsync(baseImage, new MaterialMockupRequest
                {
                    DeviceId = string.IsNullOrWhiteSpace(item.MaterialDevice) ? "iphone-16-pro-max" : item.MaterialDevice,
                    TimeMode = string.IsNullOrWhiteSpace(item.MaterialTimeMode) ? "now" : item.MaterialTimeMode,
                    Time = item.MaterialTime,
                    Scale = item.MaterialScale <= 0 ? 1 : item.MaterialScale,
                    Notification = string.IsNullOrWhiteSpace(item.MaterialNotification) ? "none" : item.MaterialNotification
                }, cancellationToken);
                generated = true;
                if (writeLog != null) await writeLog(logLabel + " 生成素材成功");
                await using var generatedStream = new MemoryStream(png);
                var sent = await _tools.SendPhotoToResolvedChatAsync(accountId, target, generatedStream, "material.png", text, null, cancellationToken);
                if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃素材图片发送失败");
                materialCount++;
                if (!string.IsNullOrWhiteSpace(text)) textCount++;
            }
            else if (!string.IsNullOrWhiteSpace(imageToken))
            {
                var asset = await _templates.ResolveImageTemplateAsync(imageToken, cancellationToken);
                await using var image = await _assets.OpenReadAsync(asset.AssetPath, cancellationToken);
                var sent = await _tools.SendPhotoToResolvedChatAsync(accountId, target, image, asset.FileName, text, null, cancellationToken);
                if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃图片发送失败");
                imageCount++;
                if (!string.IsNullOrWhiteSpace(text)) textCount++;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                var sent = await _tools.SendMessageToResolvedChatAsync(accountId, target, text, cancellationToken: cancellationToken);
                if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃消息发送失败");
                textCount++;
            }
        }

        await sendOne(rule);
        foreach (var extraText in rule.ExtraTexts ?? [])
            await sendOne(new CustomerGroupEngagementTaskHandler.MessageRule { Text = extraText });
        foreach (var extraImage in rule.ExtraImages ?? [])
            await sendOne(extraImage);

        if (writeLog != null)
            await writeLog(logLabel + " 发送成功 文字" + textCount + "|图片" + imageCount + "|素材图" + materialCount);

        return new EngagementRuleSendResult
        {
            TextCount = textCount,
            ImageCount = imageCount,
            MaterialCount = materialCount,
            GeneratedMaterial = generated
        };
    }
}
