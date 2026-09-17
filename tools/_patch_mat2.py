from pathlib import Path
p = Path('src/TelegramPanel.Web/Services/CustomerGroupEngagementTaskHandler.cs')
t = p.read_text(encoding='utf-8')
old = '''                    var templateRendering = host.Services.GetRequiredService<TemplateRenderingService>();
                    var assetStorage = host.Services.GetRequiredService<ImageAssetStorageService>();
                    var rules = config.MessageRules.Count > 0
                        ? config.MessageRules
                        : config.ActivityMessages.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => new MessageRule { Text = x }).ToList();
                    if (rules.Count == 0) rules.Add(new MessageRule { Text = "Hello" });
                    foreach (var rule in rules)
                    {
                        var text = string.IsNullOrWhiteSpace(rule.Text) ? string.Empty : await templateRendering.RenderTextTemplateAsync(rule.Text, ct);
                        if (!string.IsNullOrWhiteSpace(rule.ImageDictionaryToken))
                        {
                            var asset = await templateRendering.ResolveImageTemplateAsync(rule.ImageDictionaryToken, ct);
                            await using var image = await assetStorage.OpenReadAsync(asset.AssetPath, ct);
                            var sent = await tools.SendPhotoToResolvedChatAsync(accountId, resolved.Target, image, asset.FileName, text, null, ct);
                            if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃图片发送失败");
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(text)) continue;
                            var sent = await tools.SendMessageToResolvedChatAsync(accountId, resolved.Target, text, cancellationToken: ct);
                            if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃消息发送失败");
                        }
                        await Delay(config, ct);
                    }'''
new = '''                    var templateRendering = host.Services.GetRequiredService<TemplateRenderingService>();
                    var assetStorage = host.Services.GetRequiredService<ImageAssetStorageService>();
                    var mockup = host.Services.GetRequiredService<MaterialMockupService>();
                    var rules = config.MessageRules.Count > 0
                        ? config.MessageRules
                        : config.ActivityMessages.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => new MessageRule { Text = x }).ToList();
                    if (rules.Count == 0) rules.Add(new MessageRule { Text = "Hello" });
                    foreach (var rule in rules)
                    {
                        var text = string.IsNullOrWhiteSpace(rule.Text) ? string.Empty : await templateRendering.RenderTextTemplateAsync(rule.Text, ct);
                        var materialToken = rule.MaterialDictionaryToken;
                        var imageToken = rule.ImageDictionaryToken;
                        if (!string.IsNullOrWhiteSpace(materialToken))
                        {
                            var asset = await templateRendering.ResolveImageTemplateAsync(materialToken, ct);
                            await using var baseImage = await assetStorage.OpenReadAsync(asset.AssetPath, ct);
                            var png = await mockup.ComposeAsync(baseImage, new MaterialMockupRequest
                            {
                                DeviceId = string.IsNullOrWhiteSpace(rule.MaterialDevice) ? "iphone-16-pro-max" : rule.MaterialDevice,
                                TimeMode = string.IsNullOrWhiteSpace(rule.MaterialTimeMode) ? "now" : rule.MaterialTimeMode,
                                Time = rule.MaterialTime,
                                Scale = rule.MaterialScale <= 0 ? 1 : rule.MaterialScale,
                                Notification = string.IsNullOrWhiteSpace(rule.MaterialNotification) ? "none" : rule.MaterialNotification
                            }, ct);
                            await using var generated = new MemoryStream(png);
                            var sent = await tools.SendPhotoToResolvedChatAsync(accountId, resolved.Target, generated, "material.png", text, null, ct);
                            if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃素材图片发送失败");
                        }
                        else if (!string.IsNullOrWhiteSpace(imageToken))
                        {
                            var asset = await templateRendering.ResolveImageTemplateAsync(imageToken, ct);
                            await using var image = await assetStorage.OpenReadAsync(asset.AssetPath, ct);
                            var sent = await tools.SendPhotoToResolvedChatAsync(accountId, resolved.Target, image, asset.FileName, text, null, ct);
                            if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃图片发送失败");
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(text)) continue;
                            var sent = await tools.SendMessageToResolvedChatAsync(accountId, resolved.Target, text, cancellationToken: ct);
                            if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃消息发送失败");
                        }
                        await Delay(config, ct);
                    }'''
if old not in t: raise SystemExit('send loop missing')
p.write_text(t.replace(old, new, 1), encoding='utf-8')
print('handler send ok')
