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
2. 上传 `command-console.tpm`，确认模块 ID 为 `command-console`、版本为 `1.0.0`。
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
```

`delay` 单位为毫秒；`group.invite` 会逐个执行，单个用户失败不会覆盖同批次其他用户结果。权限不在本地预判，以 Telegram 实际 RPC 返回为准。

查询运行列表：`GET /api/panel/extensions/command-console/runs`

查询完整 JSONL 事件：`GET /api/panel/extensions/command-console/runs/{run_id}`

运行事件包括 `run.started`、`command.parsed`、`step.started`、`step.succeeded`、`step.failed`、`run.succeeded` 或 `run.failed`。失败事件保留异常消息；日志不会写入 ApiHash、Session 内容、密码或代理凭据。
