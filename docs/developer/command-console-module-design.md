# 命令控制台模块设计

## 目标

提供一个独立模块包，通过命令执行账号、群组、频道和用户操作。每次运行必须持久化完整上下文与步骤事件，使成功路径可复核、失败路径保留原始错误。

模块更新流程是“上传新版模块包并重启宿主服务”，不要求重新构建主程序镜像。当前宿主是同进程插件系统，不承诺单独卸载程序集后的无重启热更新。

## 第一版命令

```text
account.sync account=2
chat.join account=2 target=https://t.me/example
chat.leave account=2 target=https://t.me/example
group.list account=2 refresh=true
group.invite account=2 group=123456 usernames=@alice,@bob delay=2000
channel.list account=2 refresh=true
telegram.raw account=2 method=contacts.resolveUsername args={"username":"example"}
```

`telegram.raw` 第一版只开放白名单方法，不能接受任意反射调用。

## 命令格式

命令由动作名和 `key=value` 参数组成。包含空格或 JSON 的值使用双引号；批量值支持逗号或换行。

```text
group.invite account=2 group=123456 usernames="@alice,@bob" delay=2000
```

解析后立即输出标准化命令，不静默修改输入：

```text
========== COMMAND ==========
run_id: 01J...
raw: group.invite account=2 group=123456 usernames="@alice,@bob" delay=2000
normalized_action: group.invite
account_id: 2
target_group_id: 123456
targets: ["@alice", "@bob"]
delay_ms: 2000
=============================
```

## 上下文输出合同

每次运行包含不可缺少的运行上下文：

- `run_id`：运行唯一标识。
- `command`：原始命令和标准化命令。
- `module_version`、`host_version`。
- `task_id`：如果通过任务中心执行。
- `account_id`、账号显示编号、Session 路径标识；不得输出 Session 内容。
- `proxy_mode`、代理 ID 和出口摘要；不得输出代理密码。
- `api_id`；不得输出 ApiHash。
- `target_raw`、`target_type`、标准化目标。
- `step_id`、开始时间、结束时间、耗时。
- 成功返回的 Telegram ID、标题、用户名和关键计数。
- 失败的异常类型、RPC code、RPC 原文、调用阶段和重试次数。

## 步骤事件

控制台文本和持久化 JSONL 使用同一事件源。事件类型：

| 类型 | 含义 |
| --- | --- |
| `run.started` | 运行开始和完整上下文 |
| `command.parsed` | 命令解析结果 |
| `step.started` | 一个明确步骤开始 |
| `step.succeeded` | 步骤成功及关键返回值 |
| `step.failed` | 步骤失败及原始错误 |
| `step.retrying` | 重试原因与等待时间 |
| `run.succeeded` | 最终成功摘要 |
| `run.failed` | 最终失败摘要 |
| `run.canceled` | 用户取消或宿主停止 |

JSONL 示例：

```json
{"type":"step.started","runId":"01J...","stepId":"resolve-target","timeUtc":"2026-09-22T03:00:00Z","message":"解析公开群组用户名","data":{"raw":"https://t.me/example","username":"example"}}
{"type":"step.failed","runId":"01J...","stepId":"resolve-target","timeUtc":"2026-09-22T03:00:01Z","message":"Telegram 请求失败","error":{"exceptionType":"TL.RpcException","rpcCode":400,"rpcMessage":"USERNAME_NOT_OCCUPIED","raw":"400 USERNAME_NOT_OCCUPIED"}}
```

错误事件必须同时保存可读说明和原始错误。不能用“操作失败”覆盖 `RpcException.Message`。

## 存储

模块数据放在宿主持久目录：

```text
/data/modules/data/command-console/
  runs/
    2026-09/
      <run-id>.jsonl
  index.db
  settings.json
```

- JSONL 是原始审计记录，追加写入。
- SQLite 索引用于运行列表、筛选和分页，不替代 JSONL 原始记录。
- `BatchTaskLogs` 保存任务中心摘要；完整步骤保存在模块目录。
- 单条事件限制大小，敏感字段按键名过滤。

## 执行结构

```text
CommandParser
  -> CommandValidator
  -> CommandExecutionContextFactory
  -> CommandDispatcher
  -> ICommandHandler
  -> Host Telegram services
  -> CommandEventSink
  -> JSONL + index + BatchTaskLogs
```

模块复用宿主服务：

- `AccountTelegramToolsService`
- `GroupService`
- `ChannelService`
- `DataSyncService`
- `ITelegramClientPool`

模块不得自行创建 `WTelegram.Client`，否则会绕过账号代理和连接生命周期。

## 群组邀请行为

`group.invite` 不根据本地 `IsAdmin` 或权限快照阻止执行。它直接逐个调用 Telegram，并记录真实结果：

```text
---------- INVITE 1/2 ----------
account: #2
group: 123456
target: @alice
result: success
telegram_user_id: 998877
elapsed_ms: 842
--------------------------------

---------- INVITE 2/2 ----------
account: #2
group: 123456
target: @bob
result: failed
rpc_code: 400
rpc_message: USER_PRIVACY_RESTRICTED
raw_error: 400 USER_PRIVACY_RESTRICTED
elapsed_ms: 311
--------------------------------
```

失败不会中止整个批次，除非错误表示账号 Session、连接或权限整体不可用。最终摘要显示成功、失败、已在群中和未确认数量。

## 模块边界

- 模块安装、启用、停用和切换版本通常需要重启宿主服务。
- 模块设置、命令和运行记录即时生效，不需要重启。
- 模块引用宿主 Core/Web 服务时必须锁定兼容宿主版本。
- 模块包不能携带自己的 `TelegramPanel.Core.dll`。
- 第一版不提供任意 shell、SQL 或任意 Telegram 方法反射入口。

## 实施顺序

1. 建立外部模块项目、manifest 和打包脚本。
2. 实现命令解析器、上下文对象和事件存储。
3. 实现 `account.sync`、`chat.join`、`group.list`。
4. 实现不预判权限的 `group.invite`。
5. 增加最小管理 API：提交命令、读取运行列表、读取事件、取消运行。
6. 增加简单控制台页面。
7. 用模块包安装、重启服务和历史回放进行验收。

## 验收标准

- 每个命令都有 `run.started` 和一个终态事件。
- 每个 Telegram 调用都有步骤名、耗时和原始失败信息。
- 成功邀请和失败邀请能够在同一批次中分别查看。
- 宿主重启后历史仍可读取。
- 模块升级不需要重建主程序镜像。
- 日志中不出现 ApiHash、Session 内容、密码或代理凭据。
