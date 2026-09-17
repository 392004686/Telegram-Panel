from pathlib import Path

def sub(path, old, new):
    t = Path(path).read_text(encoding='utf-8')
    if old not in t:
        raise SystemExit('MISS '+path+' :: '+old[:80])
    Path(path).write_text(t.replace(old, new, 1), encoding='utf-8')
    print('ok', path)

sub('src/TelegramPanel.Data/AppDbContext.cs',
    '    public DbSet<BatchTask> BatchTasks => Set<BatchTask>();',
    '    public DbSet<BatchTask> BatchTasks => Set<BatchTask>();\n    public DbSet<BatchTaskLog> BatchTaskLogs => Set<BatchTaskLog>();')

sub('src/TelegramPanel.Web/Api/PanelAdminApiEndpoints.cs',
    '        secured.MapGet("/tasks/{id:int}", GetTaskAsync);',
    '        secured.MapGet("/tasks/{id:int}", GetTaskAsync);\n        secured.MapGet("/tasks/{id:int}/logs", GetTaskLogsAsync);\n        secured.MapGet("/tasks/{id:int}/logs.csv", ExportTaskLogsCsvAsync);')

sub('frontend/src/views/ChatResources.vue',
    "        <el-table-column v-if=\"isColumnVisible('username')\" label=\"用户名\" min-width=\"140\">",
    "        <el-table-column v-if=\"kind !== 'group' && isColumnVisible('username')\" label=\"用户名\" min-width=\"140\">")

sub('frontend/src/config/appVersion.ts',
    "export const APP_RELEASE_VERSION = '1.0.2'",
    "export const APP_RELEASE_VERSION = '1.0.3'")
