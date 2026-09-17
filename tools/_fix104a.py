from pathlib import Path

p = Path('src/TelegramPanel.Web/Services/CustomerGroupEngagementTaskHandler.cs')
t = p.read_text(encoding='utf-8')
old = '''            logger.LogInformation("从分类 {CategoryId} 加载到 {Count} 个可用执行账号",
                config.AccountCategoryId, config.AccountIds.Count);
        }'''
new = '''            logger.LogInformation("从分类 {CategoryId} 加载到 {Count} 个可用执行账号",
                config.AccountCategoryId, config.AccountIds.Count);
            await WriteTaskLog("info", $"从账号分类 #{config.AccountCategoryId} 加载 {config.AccountIds.Count} 个可用执行账号");
        }'''
if old not in t: raise SystemExit('account log missing')
t = t.replace(old, new, 1)
old = '''        var customers = await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);

        if (config.AssignmentMode == "random")
        {
            customers = customers.OrderBy(_ => Guid.NewGuid()).ToList();
            logger.LogInformation("使用随机分配模式");
        }

        logger.LogInformation("加载到 {Count} 个待邀请客户", customers.Count);

        if (customers.Count == 0)
        {
            logger.LogWarning("没有找到待邀请的客户");
            return;
        }'''
new = '''        var customers = await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var contactedQuery = db.Customers.AsNoTracking().Where(x => x.InteractionStatus == "contacted");
        if (config.CustomerGroupIds.Count > 0)
            contactedQuery = contactedQuery.Where(x => x.GroupAssignments.Any(g => config.CustomerGroupIds.Contains(g.CustomerGroupId)));
        var contactedCount = await contactedQuery.CountAsync(cancellationToken);

        if (config.AssignmentMode == "random")
        {
            customers = customers.OrderBy(_ => Guid.NewGuid()).ToList();
            logger.LogInformation("使用随机分配模式");
        }

        logger.LogInformation("加载到 {Count} 个待邀请客户，已沟通跳过 {Contacted}", customers.Count, contactedCount);
        await WriteTaskLog("info", $"待邀请 {customers.Count} 人，已沟通跳过 {contactedCount} 人");

        if (customers.Count == 0)
        {
            var msg = contactedCount > 0
                ? $"没有待邀请客户：所选分类里 {contactedCount} 人全部已是「已沟通」，任务不会重复建群邀请。请先在客户列表改回未执行，或换一个还有未沟通客户的分类。"
                : "没有待邀请客户：所选客户分类为空。";
            logger.LogWarning("{Message}", msg);
            await WriteTaskLog("error", msg);
            throw new InvalidOperationException(msg);
        }'''
if old not in t: raise SystemExit('empty customer block missing')
t = t.replace(old, new, 1)
p.write_text(t, encoding='utf-8')
print('handler ok')
