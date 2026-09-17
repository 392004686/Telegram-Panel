from pathlib import Path
p = Path('src/TelegramPanel.Web/Services/CustomerGroupEngagementTaskHandler.cs')
t = p.read_text(encoding='utf-8')
old = '                    logger.LogInformation("群组 {TelegramId} 成功邀请 {Count} 人，开始发送活跃消息",\n                        info.TelegramId, invited.Count);'
new = '                    saved.MemberCount = Math.Max(saved.MemberCount, 1 + invited.Count);\n                    saved.SyncedAt = DateTime.UtcNow;\n                    await groupManagement.CreateOrUpdateGroupAsync(saved);\n                    logger.LogInformation("群组 {TelegramId} 成功邀请 {Count} 人，开始发送活跃消息",\n                        info.TelegramId, invited.Count);\n                    db.BatchTaskLogs.Add(new BatchTaskLog { BatchTaskId = host.TaskId, Level = "info", Message = "群 " + info.TelegramId + " 邀请成功 " + invited.Count + " 人，成员数 " + saved.MemberCount, CreatedAt = DateTime.UtcNow });\n                    await db.SaveChangesAsync(ct);'
if old not in t:
    i = t.find('成功邀请')
    raise SystemExit('invite missing: '+(repr(t[i:i+180]) if i>=0 else 'none'))
t = t.replace(old, new, 1)
old = '        logger.LogInformation("开始执行批量建群邀请客户任务，任务ID: {TaskId}", host.TaskId);'
new = '        logger.LogInformation("开始执行批量建群邀请客户任务，任务ID: {TaskId}", host.TaskId);\n        async Task WriteTaskLog(string level, string message)\n        {\n            var text = message.Length > 4000 ? message.Substring(0, 4000) : message;\n            db.BatchTaskLogs.Add(new BatchTaskLog { BatchTaskId = host.TaskId, Level = level, Message = text, CreatedAt = DateTime.UtcNow });\n            await db.SaveChangesAsync(cancellationToken);\n        }'
if old not in t: raise SystemExit('start missing')
t = t.replace(old, new, 1)
p.write_text(t, encoding='utf-8')
print('handler patched')
