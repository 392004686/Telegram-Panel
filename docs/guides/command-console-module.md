# 命令控制台模块

## 用途（中文说明）

这是一个安装到 Telegram Panel 的命令驱动模块，用于通过后台接口执行账号同步、加入群组/频道、刷新群组列表和逐个邀请用户。它复用宿主的 Telegram 客户端池、账号代理和连接生命周期，不会自行创建 `WTelegram.Client`，也不开放任意 Shell、SQL 或任意反射调用。

每次运行都会生成 `run_id`，把原始命令、解析结果、步骤耗时、成功返回值以及 Telegram 原始错误追加保存为 JSONL。宿主重启后仍可通过运行查询接口回看历史。

## 打包

在仓库根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\package-command-console.ps1
```

产物为 `artifacts/modules/command-console.tpm`。脚本会执行 `dotnet publish`，应使用与仓库 `global.json` 匹配的 .NET 8 SDK；本机没有 SDK 时请在项目 Docker 构建环境中执行。

## 上传与启用

1. 登录后台，打开“模块管理”。
2. 上传 `command-console.tpm`，确认模块 ID 为 `command-console`、版本为 `1.1.2`。
3. 启用该版本并重启宿主服务。当前插件是同进程动态程序集，安装/启用/切换版本通常需要重启；不需要重新构建主程序镜像。
4. 重启后检查模块状态为 enabled，再调用下方接口。升级时上传新版本包并重启；不要把 `TelegramPanel.Core.dll` 打进模块包。

## 使用方法

接口前缀：`/api/panel/extensions/command-console`，需要宿主后台登录授权。

提交命令：

```http
POST /api/panel/extensions/command-console/runs
Content-Type: application/json

{"command":"group.invite account=2 group=123456 usernames=\"@alice,@bob\" delay=2000"}
```

示例命令（每行上方是用途和参数说明；示例中的 ID 需要替换成你项目里的真实 ID）：

```text
# 读取账号数据并同步到面板；会实际请求 Telegram
account.sync account=2

# 用账号 2 打开公开群组/频道链接或邀请链接；会实际加入目标。target 接受普通 URL
chat.join account=2 target=https://t.me/example

# 查询账号 2 当前可见的群组列表；该命令本身会向 Telegram 拉取最新对话列表
group.list account=2

# 将两个客户逐个邀请到系统已同步的群组；group 可填系统群组 ID 或 Telegram 群组 ID，不能填示例占位数字
group.invite account=2 group=-1001234567890 usernames="@alice,@bob" delay=2000

# 查看系统账号 27 的启用、Telegram 状态、API ID 和代理出口
engagement.account.health accountId=27

# 用账号 27 检查 Telegram 群组 -1001234567890 的成员快照、客户/未知成员数和可复用状态
engagement.group.inspect accountId=27 telegramGroupId=-1001234567890

# 列出可供账号 27 使用的群组，并显示每个群组可用或跳过的原因
engagement.group.available accountId=27

# 检查账号 27 在指定群中的成员情况；群内有客户时会拒绝退出清理
engagement.group.cleanup accountId=27 telegramGroupId=-1001234567890

# 只预览客户分类 8 中最多 3 个待执行客户，不邀请、不加入群组
engagement.batch.preview accountId=27 telegramGroupId=-1001234567890 customerCategoryId=8 customerLimit=3

# 执行邀请：最多处理 3 个客户，至少确认 1 人入群，邀请间随机间隔 30～60 秒
# 会实际加入群组、邀请客户、确认成员快照并在结束时尝试退出
engagement.batch.run accountId=27 telegramGroupId=-1001234567890 customerCategoryId=8 inviteCount=3 minSuccess=1 delayMin=30 delayMax=60
```

> PowerShell 中 `#` 是注释。如果通过 HTTP `POST /runs` 提交命令，JSON 的 `command` 字段只放一条命令，不要把上面的 `#` 注释一并放进去。

`delay` 单位为毫秒；`group.invite` 会逐个执行，单个用户失败不会覆盖同批次其他用户结果。`group` 可以填“群组管理”页面中的系统群组 ID，也可以直接填 Telegram 群组 ID；如果本地没有对应群组记录，该值会按 Telegram 群组 ID 传给 Telegram。像 `group=123456` 这样的占位值通常不是实际群组 ID，Telegram 会在每个目标的结果中返回“群组不存在”。权限不在本地预判，以 Telegram 实际 RPC 返回为准。

`group.list` 每次直接从 Telegram 刷新该账号可见的群组并写入本地群组库，不会触发账号资料、联系人等全量同步；不需要 `refresh=true`，旧参数不会改变行为。群组管理页面的“同步”也使用群组专用刷新，不再执行账号全量同步。

群组库会记录最后一次已观察到的状态、检查时间和检查账号。状态“可见”表示对应账号最近一次同步仍能看到该群；“账号不可见”只代表该账号在本次同步中没看到，不等同于群组已删除或全局不可用。过期的账号成员关系会清除以避免误选该账号操作，但群组本身及状态记录仍保留在群组管理中。公开链接可以直接从用户名确定；私有邀请链接在首次生成/复制后缓存，列表与详情页后续复用该链接，尚未生成时显示为空。

每条命令执行完成后会立即刷新结果框；多行命令会按“第 n / 总数”分别分段，并显示该账号当前配置的代理出口和出口 IP（若探测服务不可用，会注明未取得及原因）。目前按命令完成后刷新，不是逐个 Telegram RPC 事件实时推送；后者需要单独改成流式接口。

客户批次命令使用正式业务字段：

- `accountId`：系统账号 ID；
- `telegramGroupId`：Telegram 群组 ID，当前宿主群组服务以该值执行；
- `customerCategoryId`：客户分类 ID；
- `inviteCount`：本批最多处理的客户数；
- `minSuccess`：最低确认入群人数；
- `delayMin/delayMax`：30～600 秒的随机间隔区间。

`engagement.batch.run` 执行前会检查群成员：群内出现客户或未识别外部成员时直接跳过。邀请结果只有经过成员快照确认才算成功；客户隐私、注销、用户名无效等属于客户级失败，不计执行账号失败；`FROZEN_METHOD_INVALID`、账号权限限制等属于账号级问题，会停止当前批次。首轮模块只执行邀请确认和退出群组，不自动发送活跃消息，也不会因为邀请成功直接把客户标记为“已沟通”。

模块当前是单线程运行；账号锁和群组锁在进程内有效，进程重启后的持久化租约、多线程任务恢复和消息规则仍由后续任务中心阶段实现。

查询运行列表：`GET /api/panel/extensions/command-console/runs`

查询完整 JSONL 事件：`GET /api/panel/extensions/command-console/runs/{run_id}`

运行事件包括 `run.started`、`command.parsed`、`step.started`、`step.succeeded`、`step.failed`、`run.succeeded` 或 `run.failed`。失败事件保留异常消息；日志不会写入 ApiHash、Session 内容、密码或代理凭据。
