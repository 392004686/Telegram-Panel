# 批量建群、邀请客户、执行活跃并标记已沟通：执行器设计

状态：功能设计完成，尚未实现  
任务类型建议：`customer_group_engagement`  
适用版本：Telegram X 自定义版

## 1. 目标与边界

该执行器把以下动作组织成一个可暂停、可恢复、可审计的后台批任务：

1. 从客服账号分类或指定账号集合中取得可用执行账号；
2. 从客户分类及查询/沟通状态筛选客户；
3. 按队列或随机模式分配客户并创建群组；
4. 邀请客户入群；
5. 按文字、图片、视频或组合规则执行群内活跃；
6. 满足任务配置的成功条件后，把对应客户标记为“已沟通”。

任务运行不依赖浏览器页面。页面关闭、路由切换、重新登录或容器重启后，任务由数据库恢复。第一版只创建私密群；公开化继续复用现有 `channel_group_publicize` 任务，避免在一个执行器中混入用户名抢占逻辑。

## 2. 创建配置

建议使用强类型 `CustomerGroupEngagementTaskConfig`，创建后固化为 JSON：

| 字段 | 含义 |
|---|---|
| `accountSource` | `accounts` 或 `category` |
| `accountIds` / `accountCategoryId` | 指定账号集合或账号分类 |
| `customerGroupIds` | 一个或多个客户分类 |
| `lookupStatuses` | `pending/found/not_found/failed`；空表示全部 |
| `interactionStatuses` | 默认只选 `uncontacted` |
| `customerLimit` | 本次最多领取客户数 |
| `customersPerGroup` | 每群计划邀请数 |
| `assignmentMode` | `queue` 或 `random` |
| `workerCount` | 并发执行账号数 |
| `allowCrossTaskReuse` | 是否允许客户被其他未结束任务重复领取，默认 `false` |
| `groupTitleTemplate` | 群名模板，支持日期、序号、账号昵称变量 |
| `groupAboutTemplate` | 群简介模板 |
| `avatarSource` | `none/fixed/dictionary` |
| `activityRules` | 有序的文字、图片、视频或组合发送规则 |
| `minDelaySeconds/maxDelaySeconds` | 单账号动作间隔，由服务端计算 |
| `minSuccessfulInvites` | 群组进入活跃阶段所需最少成功邀请数 |
| `contactedMilestone` | `invite_succeeded/first_activity_succeeded/activity_completed` |
| `maxAttempts` | 每阶段最大尝试次数 |
| `failedGroupPolicy` | `keep/archive/delete_if_empty`，第一版默认 `keep` |

`workerCount` 在任务开始时校验为 `1..可用执行账号数`，运行中账号失效只影响对应 worker，不重新计算或重置整个任务。

## 3. 持久化模型

`BatchTasks` 继续保存任务总状态、进度、心跳和配置。业务级恢复点单独落表，不能只写在 `BatchTask.Config`。

### 3.1 `CustomerEngagementRuns`

- `Id`、`BatchTaskId`（唯一）
- `Status`、`Phase`
- `ConfigSnapshotJson`
- `AvailableAccountCountAtStart`、`WorkerCount`
- `SelectedCustomerCount`、`CompletedCustomerCount`、`FailedCustomerCount`
- `CreatedGroupCount`、`CompletedGroupCount`
- `CreatedAtUtc`、`StartedAtUtc`、`CompletedAtUtc`、`HeartbeatAtUtc`

### 3.2 `CustomerEngagementGroups`

- `Id`、`RunId`、`Sequence`
- `AccountId`、`GroupId`、`TelegramGroupId`
- `IdempotencyKey`（唯一）
- `TitleSnapshot`、`AboutSnapshot`
- `Status`、`Phase`、`AttemptCount`、`LastErrorCode`、`LastError`
- `SuccessfulInviteCount`、`SuccessfulActivityCount`
- `LeaseOwner`、`LeaseExpiresAtUtc`
- 各阶段开始/完成时间

### 3.3 `CustomerEngagementAssignments`

- `Id`、`RunId`、`EngagementGroupId`、`CustomerId`
- `Sequence`、`Status`、`CurrentPhase`
- `LeaseToken`、`LeaseExpiresAtUtc`
- `InviteAttemptCount`、`InviteStatus`、`InviteErrorCode`、`InvitedAtUtc`
- `ActivityStatus`、`ActivityErrorCode`
- `ContactedAtUtc`、`CompletedAtUtc`
- 唯一索引：`(RunId, CustomerId)`

### 3.4 `CustomerEngagementEvents`

追加式审计日志：`RunId`、可空的 Group/Assignment/Account/Customer ID、`EventType`、`Result`、`ErrorCode`、已脱敏 `Message`、`OccurredAtUtc`。事件用于详情页与排障，不承担恢复状态。

## 4. 客户领取、租约与幂等

任务启动时在短事务内按筛选条件确定候选客户，并写入 assignment。默认排除：已沟通、已被其他活动任务持有有效租约、缺少 Telegram 用户标识且无法邀请的客户。

SQLite 不使用 `SELECT ... FOR UPDATE`。领取采用带条件的更新：只有 `LeaseExpiresAtUtc IS NULL OR LeaseExpiresAtUtc < now` 才写入新的随机 `LeaseToken`。提交后再次读取并核对 token，失败则由其他 worker 处理。worker 定期续租，崩溃后租约自然过期。

关键幂等键：

- 建群：`engagement:{runId}:group:{sequence}`；数据库中先创建 planned 行，再调用 Telegram。
- 邀请：`engagement:{runId}:assignment:{assignmentId}:invite`。
- 活跃消息：`engagement:{runId}:group:{groupId}:rule:{ruleIndex}:message:{messageIndex}`。
- 标记已沟通：条件更新 `InteractionStatus != contacted`，同时写 `LastInteractionAt`。

Telegram RPC 不保证真正的 exactly-once。重启恢复时先同步群信息/成员关系，再决定是否重放；已在群中的客户按邀请成功处理，已存在的业务群记录禁止再次建群。

## 5. 状态机

```text
pending
  -> selecting_customers
  -> allocating
  -> creating_group
  -> inviting
  -> activating
  -> marking_contacted
  -> completed

任一阶段 -> waiting_flood / retry_wait -> 原阶段
任一阶段 -> paused / canceled / failed
```

assignment 状态：`reserved -> invite_pending -> invited -> activity_pending -> contacted -> completed`，失败状态细分为 `not_found/privacy_restricted/account_invalid/flood_wait/transient/permanent`。

暂停时不领取新客户；当前 Telegram RPC 返回后保存恢复点并退出。取消时释放未开始 assignment 的租约，保留已建群、已邀请和审计记录。容器启动时现有恢复服务把 `running` 任务改回 `pending`，handler 根据业务表的 `Phase` 和幂等键继续。

## 6. 执行调度

1. 解析并验证配置，生成 run 和 assignments。
2. 启动固定数量 worker，每个 worker 固定绑定一个启动时可用账号。
3. 每个 worker 一次领取一组客户，按 `customersPerGroup` 分组。
4. 建群成功即保存 `GroupId/TelegramGroupId`，再进入邀请阶段。
5. 每次 Telegram 动作前后检查 `host.IsStillRunningAsync`，动作后立即保存状态并更新心跳。
6. 每个账号独立维护下次允许执行时间；全局 worker 不共用一个 delay。
7. 群邀请达到 `minSuccessfulInvites` 后执行 activity rules，否则按失败群策略收尾。
8. 到达 `contactedMilestone` 时逐客户条件更新已沟通状态。

`BatchTask.Total` 定义为本次锁定的客户数，`Completed` 为进入终态的 assignment 数，`Failed` 为永久失败 assignment 数；群组级进度在详情 DTO 单独展示。

## 7. 错误与重试

| 类别 | 处理 |
|---|---|
| FloodWait | 保存 Telegram 指定等待时间，释放 worker 执行槽前使用可恢复延迟 |
| 短暂网络/RPC 错误 | 指数退避加抖动，累计到 `maxAttempts` 后失败 |
| 账号失效/冻结 | 停止该 worker，未开始分组交给其他可用 worker |
| 隐私限制 | assignment 永久失败，不重试邀请 |
| 用户不存在/注销 | 永久失败并记录客户查询状态 |
| 部分邀请成功 | 成功客户继续后续阶段，失败客户独立终止 |
| 活跃素材缺失 | 群组阶段失败，保留已邀请事实，不回滚客户入群 |

异常文本进入数据库前复用现有 secret/URI 脱敏和长度限制。服务端为每类错误保存稳定 `ErrorCode`，UI 不通过解析中文文本判断状态。

## 8. “已沟通”合同

默认 `contactedMilestone=activity_completed`。只有该客户邀请成功，且所在群按配置完成全部必需活跃规则后，才执行：

```text
InteractionStatus = contacted
LastInteractionAt = 当前 UTC 时间
```

查询失败、邀请失败、仅建群成功、活跃部分失败都不能标记已沟通。管理员选择较早里程碑时，任务配置和审计事件必须明确记录，详情页显示实际触发点。重复执行只更新时间还是保持首次时间需要产品统一；第一版建议保留首次成功时间，事件表记录后续任务触达。

## 9. API 与界面

- 在 `BatchTaskTypes` 和 Task Catalog 注册 `customer_group_engagement`。
- 提供专用创建/编辑组件，不使用通用 JSON 作为正式入口。
- 创建 API 在服务端重新计算可用账号数、候选客户数和最大并发。
- 任务详情展示：配置快照、worker/账号、各阶段数量、群组列表、客户分配、错误分类、最近事件。
- 支持暂停、继续、取消、失败项重试、复制任务；编辑仅允许暂停后修改尚未领取部分的延迟和活跃规则。
- 客户列表可反查最近一次触达任务和群组。

## 10. 实施拆分与验收

1. 数据层：实体、索引、迁移，验证空库 Up、生产副本 Up、Down/Up。
2. 领域层：配置验证、候选筛选、分配器、租约、状态机和错误分类。
3. Telegram 适配：建群、成员检查/邀请、素材发送，全部可用 fake service 测试。
4. handler：恢复点、每账号延迟、心跳、暂停/取消和重启恢复。
5. API/UI：专用表单、详情、失败重试和客户反查。
6. 集成验收：关闭浏览器、暂停继续、容器重启、FloodWait、账号掉线、部分成功、重复恢复。

上线验收至少包含：并发数只在启动时校验；两个任务不会重复领取同一客户；重启不重复建群；成员已存在时不会重复邀请；失败客户不会被标记已沟通；5000 新镜像健康且 7000 作者版保持不变。
