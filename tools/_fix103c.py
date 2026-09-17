from pathlib import Path
p = Path(r'src/TelegramPanel.Web/Services/CustomerGroupEngagementTaskHandler.cs')
t = p.read_text(encoding='utf-8')
old = '''                    foreach (var rule in rules)
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
new = r'''                    async Task SendRuleAsync(MessageRule item, string logLabel)
                    {
                        var text = string.IsNullOrWhiteSpace(item.Text) ? string.Empty : await templateRendering.RenderTextTemplateAsync(item.Text, ct);
                        var materialToken = item.MaterialDictionaryToken;
                        var imageToken = item.ImageDictionaryToken;
                        if (!string.IsNullOrWhiteSpace(materialToken))
                        {
                            var asset = await templateRendering.ResolveImageTemplateAsync(materialToken, ct);
                            await using var baseImage = await assetStorage.OpenReadAsync(asset.AssetPath, ct);
                            var png = await mockup.ComposeAsync(baseImage, new MaterialMockupRequest
                            {
                                DeviceId = string.IsNullOrWhiteSpace(item.MaterialDevice) ? "iphone-16-pro-max" : item.MaterialDevice,
                                TimeMode = string.IsNullOrWhiteSpace(item.MaterialTimeMode) ? "now" : item.MaterialTimeMode,
                                Time = item.MaterialTime,
                                Scale = item.MaterialScale <= 0 ? 1 : item.MaterialScale,
                                Notification = string.IsNullOrWhiteSpace(item.MaterialNotification) ? "none" : item.MaterialNotification
                            }, ct);
                            await using var generated = new MemoryStream(png);
                            var sent = await tools.SendPhotoToResolvedChatAsync(accountId, resolved.Target, generated, "material.png", text, null, ct);
                            if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃素材图片发送失败");
                            await WriteTaskLog("info", logLabel + " 已发送素材图");
                        }
                        else if (!string.IsNullOrWhiteSpace(imageToken))
                        {
                            var asset = await templateRendering.ResolveImageTemplateAsync(imageToken, ct);
                            await using var image = await assetStorage.OpenReadAsync(asset.AssetPath, ct);
                            var sent = await tools.SendPhotoToResolvedChatAsync(accountId, resolved.Target, image, asset.FileName, text, null, ct);
                            if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃图片发送失败");
                            await WriteTaskLog("info", logLabel + " 已发送图片字典");
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(text)) return;
                            var sent = await tools.SendMessageToResolvedChatAsync(accountId, resolved.Target, text, cancellationToken: ct);
                            if (!sent.Success) throw new InvalidOperationException(sent.Error ?? "活跃消息发送失败");
                            await WriteTaskLog("info", logLabel + " 已发送文字");
                        }
                        await Delay(config, ct);
                    }
                    var ruleIndex = 0;
                    foreach (var rule in rules)
                    {
                        ruleIndex++;
                        await SendRuleAsync(rule, "规则" + ruleIndex);
                        var extraTextIndex = 0;
                        foreach (var extraText in rule.ExtraTexts ?? [])
                        {
                            extraTextIndex++;
                            await SendRuleAsync(new MessageRule { Text = extraText }, "规则" + ruleIndex + " 附加文字" + extraTextIndex);
                        }
                        var extraImageIndex = 0;
                        foreach (var extraImage in rule.ExtraImages ?? [])
                        {
                            extraImageIndex++;
                            await SendRuleAsync(extraImage, "规则" + ruleIndex + " 附加图片" + extraImageIndex);
                        }
                    }'''
if old not in t: raise SystemExit('send loop missing')
t = t.replace(old, new, 1)
old = '''                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    logger.LogError(ex, "创建群组或邀请过程中发生异常");'''
new = '''                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    logger.LogError(ex, "创建群组或邀请过程中发生异常");
                    await WriteTaskLog("error", "群处理失败: " + ex.Message);'''
if old not in t: raise SystemExit('catch missing')
t = t.replace(old, new, 1)
old = '''        logger.LogInformation("开始执行批量建群邀请客户任务，任务ID: {TaskId}", host.TaskId);
        async Task WriteTaskLog(string level, string message)
        {
            var text = message.Length > 4000 ? message.Substring(0, 4000) : message;
            db.BatchTaskLogs.Add(new BatchTaskLog { BatchTaskId = host.TaskId, Level = level, Message = text, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
        }

        var config = JsonSerializer.Deserialize<Config>(host.Config ?? "{}", JsonOptions)'''
new = '''        logger.LogInformation("开始执行批量建群邀请客户任务，任务ID: {TaskId}", host.TaskId);

        var config = JsonSerializer.Deserialize<Config>(host.Config ?? "{}", JsonOptions)'''
if old not in t: raise SystemExit('early log missing')
t = t.replace(old, new, 1)
old = '''        var db = host.Services.GetRequiredService<AppDbContext>();
        var taskManagement = host.Services.GetRequiredService<BatchTaskManagementService>();'''
new = '''        var db = host.Services.GetRequiredService<AppDbContext>();
        async Task WriteTaskLog(string level, string message)
        {
            var text = message.Length > 4000 ? message.Substring(0, 4000) : message;
            db.BatchTaskLogs.Add(new BatchTaskLog { BatchTaskId = host.TaskId, Level = level, Message = text, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
        }
        await WriteTaskLog("info", "任务开始执行");
        var taskManagement = host.Services.GetRequiredService<BatchTaskManagementService>();'''
if old not in t: raise SystemExit('db init missing')
t = t.replace(old, new, 1)
old = '''        logger.LogInformation("批量建群邀请任务完成，已完成 {Completed}，失败 {Failed}", completed, failed);'''
new = '''        logger.LogInformation("批量建群邀请任务完成，已完成 {Completed}，失败 {Failed}", completed, failed);
        await WriteTaskLog("info", $"任务结束，完成 {completed}，失败 {failed}");'''
if old not in t: raise SystemExit('complete log missing')
t = t.replace(old, new, 1)
p.write_text(t, encoding='utf-8')
print('handler ok')
