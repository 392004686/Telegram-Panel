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
2. 上传 `command-console.tpm`，确认模块 ID 为 `command-console`、版本为 `1.1.0`。
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

支持命令：

```text
account.sync account=2
chat.join account=2 target=https://t.me/example
group.list account=2 refresh=true
group.invite account=2 group=123456 usernames="@alice,@bob" delay=2000

engagement.account.health accountId=27
engagement.group.inspect accountId=27 telegramGroupId=-1001234567890
engagement.group.available accountId=27
engagement.group.cleanup accountId=27 telegramGroupId=-1001234567890
engagement.batch.preview accountId=27 telegramGroupId=-1001234567890 customerCategoryId=8 customerLimit=3
engagement.batch.run accountId=27 telegramGroupId=-1001234567890 customerCategoryId=8 inviteCount=3 minSuccess=1 delayMin=30 delayMax=60
```

`delay` 单位为毫秒；`group.invite` 会逐个执行，单个用户失败不会覆盖同批次其他用户结果。权限不在本地预判，以 Telegram 实际 RPC 返回为准。

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
