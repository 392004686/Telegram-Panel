# Telegram X 项目交接文档

更新时间：2026-09-16  
仓库：`E:\Desktop\chongzhi\telegram_tool\work\Telegram-Panel`  
当前分支：`codex/multi-user-ui`  
远端：私人 fork `origin`  
远端服务：`16.216.65.42`

## 1. 当前在做什么

基于上游 Telegram Panel 持续开发自定义版 **Telegram X**，重点完善：

1. 多账号管理、设备指纹、代理及 Telegram 状态；
2. 客户管理、客户分类、客户资料查询；
3. 将“账号筛选”从临时页面请求改造成可持久化、可恢复、可在任务中心跟踪的批处理系统；
4. 后续增加“批量建群 → 邀请客户 → 群内活跃 → 标记已沟通”的正式任务执行器；
5. 自定义版运行在服务器 5000 端口，上游作者版对照容器运行在 7000 端口。

本轮用户要求仅记录问题并结束会话，**不要在本轮继续修改或部署**。下一轮从本文开始检查代码和复现。

## 2. 已完成内容

### 2.1 已有主要功能

- 品牌改为 Telegram X，隐藏上游作者版本、更新入口及 GitHub 信息。
- 只读用户首次登录可修改密码。
- 左侧导航父级选中颜色、账号表格操作列、任务输入框等做过多轮 UI 修复。
- 支持账号 ZIP 导入时读取/兼容设备指纹并复用。
- 数据字典已有图片、视频等扩展。
- 群组/频道详情支持文字、图片、视频及合并发送。
- 已创建并长期保留作者版 7000 端口对照容器。

### 2.2 客户管理已完成部分

功能提交：`df694fa feat: 重构客户筛选并修复页面网关错误`  
部署记录提交：`6f95074 docs: 记录客户筛选部署`

已实现：

- 客户管理父子导航：
  - 客户列表 `/ui/customers`
  - 账号筛选 `/ui/customers/lookup`
  - 客户分类 `/ui/customers/categories`
- 客户、客户分类、导入批次及关联数据表。
- 客户批量导入、批次、分类、筛选、批量改分类、批量删除。
- 查询输入兼容：
  - `+1 123456789`
  - `+1123456789`
  - `+1 2 3 4 56789`
  - 纯数字
  - `@username`
  - 手机号除空格和开头 `+` 外拒绝其他字符。
- 账号筛选支持指定账号/账号分类、队列/随机、最小/最大间隔。
- Telegram 查询返回字段已扩展：用户 ID、姓名、用户名、手机号、头像、活跃状态、Premium、机器人、认证、诈骗、虚假、注销、受限、生日、最后上线时间。
- 活跃状态已中文化：当前在线、最后上线时间、最近上线、一周内上线、一个月内上线、状态未知/不可见。
- 数据库迁移：`20260915150000_AddCustomerTelegramProfile`。
- GET 请求遇到 502/503/504 会退避重试两次；客户主数据和分类/批次元数据改为独立加载，失败时保留已成功数据。
- 服务端已尝试过滤临时联系人占位名 `Lookup Contact` 和 `Telegram Lookup`。

### 2.3 当前部署状态

- 5000 自定义版镜像：`telegram-panel:multi-user-ui-df694fa`
- 镜像 ID：`sha256:9918865e480967ee773b14419aa9e588b889f083e04a24cd34eeeed07450eeed`
- 5000 `/healthz`、`/ui/customers`、`/ui/customers/lookup` 部署时均为 HTTP 200。
- 7000 作者版 `/healthz` 为 HTTP 200，未被修改。
- 部署前备份：`/root/telegram-panel-data-.tar.gz`
- 备份 SHA-256：`06ab691cf8436d5be88e173525ee26719061ae01aaa7bb96f87b050ed5d98cf4`
- 注意：备份文件名缺时间戳，原因见“踩坑”。
- 服务器连接与部署信息保存在：
  `E:\Desktop\chongzhi\telegram_tool\docs\telegram-panel-server-deployment.md`

## 3. 当前确认的 Bug / 新需求

### 3.1 客户列表 UI

截图：`codex-clipboard-aca3965b-86af-4f28-846c-e81eed8527bc.png`

1. “批量导入”所在操作栏没有随内容区正确收缩/对齐，应与账号列表一致。
2. 增加“全选本页”。语义必须是当前分页可见记录，不是数据库全部记录。
3. 客户列表与之前账号列表出现过相同问题：鼠标悬停或横向滚动后，固定在右侧的操作列被内容覆盖/溢出。
4. 修复方式应复用账号列表已经验证过的 fixed-column 样式，而不是再造一套临时 CSS。
5. 宽屏和窄屏都要验证，至少检查：1366、1920、超宽屏、浏览器缩放 80%/100%。

### 3.2 客户资料没有被查询结果正确覆盖

截图中客户列表仍显示错误姓名 `Lookup Contact`，而账号筛选新结果姓名为空。

预期逻辑：

1. 已存在客户再次查询成功时，**本次 Telegram 返回数据应更新现有客户完整资料**。
2. 旧的 `Lookup Contact` / `Telegram Lookup` 等临时占位姓名必须清空或由真实姓名替换。
3. Telegram 本次明确返回空值时，需要区分：
   - 字段真实不可见/为空：清除历史占位或陈旧数据；
   - 查询接口本次没有请求到该字段：保留旧的真实值。
4. 不能继续使用目前大量 `string.IsNullOrWhiteSpace(result.X) ? old : new` 的模糊合并方式；建议给查询 DTO 增加字段可用性/来源标记，或统一定义“成功完整查询”的覆盖合同。
5. 新增客户资料字段：
   - `昵称`
   - `最后数据同步`
6. 明确“姓名”和“昵称”的定义：建议姓名=`first_name + last_name`，昵称为业务侧备注/展示昵称；不要互相覆盖。
7. `最后数据同步`应记录成功取得 Telegram 用户资料的 UTC 时间；查询失败不能伪装成同步成功。
8. 数据库、API DTO、客户列表、详情弹窗、账号筛选结果、迁移和测试都要同步更新。

### 3.3 账号筛选的执行账号区域

截图：`codex-clipboard-34346a8c-fa78-45b7-a732-55899ec4aed7.png`

用户要求执行账号展示补充：

- 昵称
- 最后数据同步
- 操作列：删除
- 与客户管理一样支持多选

下一轮需要先从现有页面和用户描述确定最终交互。推荐解释：

1. 将单一执行账号下拉框升级为可筛选账号表/选择器；
2. 指定账号模式可多选多个执行账号，批量查询按所选账号轮询；
3. 表格字段至少为：选择、手机号、昵称、用户名、Telegram 状态、最后数据同步、操作；
4. “删除”必须确认到底是从本次执行队列移除，还是删除系统账号。推荐默认做“从本次执行队列移除”，避免在查询页误删系统账号；若要删除系统账号必须复用账号管理删除接口、二次确认和审计记录；
5. 账号分类模式应展示该分类当前可执行账号数量，并排除失效/废号；
6. 增加全选本页/取消全选，选择结果跨分页是否保留必须有明确规则。

### 3.4 当前查询结果必须持久化

现状：`frontend/src/views/CustomerLookup.vue` 将结果只放在 Vue 内存数组中，切换页面即丢失。

要求：

1. 新建账号筛选查询批次和查询明细数据库表。
2. 页面刷新、切换路由、重新登录后仍可查看历史结果。
3. 建议数据结构：

- `CustomerLookupBatch`
  - Id、Name、Mode(realtime/task)、AccountSource、AccountIds/CategoryId
  - TargetOrder、MinDelaySeconds、MaxDelaySeconds
  - Status、Total、Completed、Found、NotFound、Failed
  - CreatedBy、CreatedAt、StartedAt、CompletedAt、LastHeartbeatAt
- `CustomerLookupItem`
  - BatchId、RawTarget、NormalizedTarget、Sequence
  - AccountId、CustomerId、ExistingCustomer
  - Status、Error、AttemptCount
  - 查询返回的完整用户资料快照
  - StartedAt、CompletedAt

4. 当前查询页增加：历史批次、状态筛选、查看明细、失败重试、删除记录。
5. 只有客户列表已存在的客户才自动更新客户表；未收录用户留在查询结果库，并提供明确的“加入客户列表”操作。
6. 查询批次/明细保留策略后续可配置，先默认永久保留并支持手动删除。

### 3.5 “发送任务”必须成为真正后台任务

当前严重问题：页面按钮“发送任务”实际上仍调用前端循环，关闭页面会中断。

要求：

1. 点击“发送任务”后由后端创建持久化批任务。
2. 任务必须进入任务中心，可暂停、继续、取消、失败重试、查看进度与错误。
3. 浏览器关闭、路由切换、容器重启后任务状态和未完成项目仍可恢复。
4. 执行账号按指定账号集合或账号分类轮询。
5. 每个任务项落库后再执行；不要依靠内存数组作为唯一状态。
6. 每次执行更新心跳、当前账号、当前目标、已完成/失败数量。
7. Telegram FloodWait、短暂网络错误、账号失效、隐私不可见、目标不存在必须分开分类。
8. 最小/最大间隔由服务端计算，前端值仅作为任务配置。
9. “实时查询”可保留给少量目标，但结果同样必须写查询批次/明细数据库。
10. 完成真实后台任务前，不能继续把页面内循环称为“发送任务”。

### 3.6 新增正式任务执行器

任务名称：`批量群组批量创建自动邀请客户并执行活跃`

目标流程：

1. 选择客服/执行账号分类；
2. 选择客户分类及客户状态，可选全部、已查询、未查询等；
3. 设置客户数量、每群邀请数量；
4. 目标/分配模式：随机或队列；
5. 执行账号创建群聊；
6. 将分配客户邀请入群；
7. 邀请完成后按配置执行群内活跃发言；
8. 达到成功条件后标记客户“已沟通”；
9. 每个客服账号独立延迟计时；
10. 可设置并发线程，最大线程数不得超过启动时可用执行账号数量；只在任务开始时校验，执行过程中不反复因账号数变化重置整个任务。

示例合同：A 组 10 个客服，客户组 100 个未查询客户 + 10 个已查询客户；可选整个组 110 人，由 10 个客服随机/队列分配，每人邀请 3 个，邀请后按文案活跃，成功后标记已沟通。

正式设计还需补齐：

- 群名称/简介/头像模板和变量；
- 公开群或私密群；
- 建群失败、邀请失败、用户隐私限制、FloodWait 的重试/跳过策略；
- 每群最小成功邀请数及部分成功定义；
- 客户是否允许被多个任务重复使用；
- 客户锁定/租约，防并发任务重复分配；
- “已沟通”是在邀请成功、首次发言成功还是整个群活跃完成后写入；
- 活跃内容来源：文字字典、图片字典、视频字典或组合规则；
- 失败群清理策略；
- 账号掉线后的任务接管策略；
- 任务幂等键、重启恢复点和审计日志。

## 4. 当前卡点

1. 账号筛选还没有正式后端任务处理器和持久化结果表。
2. “发送任务”只是前端循环，是当前最优先需要修正的架构问题。
3. Telegram 手机号查询通过临时联系人导入，返回的姓名可能是本地占位值；虽然已有字符串过滤，但旧脏数据不会自动清理，且空值覆盖规则不完整。
4. 客户资料中的“姓名/昵称/最后数据同步”尚未完整建模。
5. 执行账号多选和“删除”的业务含义有歧义，落地时优先按“移出本次选择”实现，并在 UI 明确措辞。
6. 大型“建群邀请并活跃”任务尚未进入代码实现，先完成任务状态机、锁定策略和可恢复性设计再接 Telegram 动作。

## 5. 下一步实施顺序

### 阶段 A：修复现有页面和数据正确性

1. 复现三个截图问题，检查 `Customers.vue`、`CustomerLookup.vue`、全局布局和账号列表 fixed-column CSS。
2. 修客户列表操作栏收缩、全选本页、右侧操作列遮挡。
3. 增加 Nickname、LastDataSyncAt 字段及迁移。
4. 重构 `ApplyLookup` 覆盖规则，编写旧占位姓名清理迁移/一次性修复。
5. 执行账号选择器升级为多选表格并补字段。
6. 添加前端和后端回归测试。

### 阶段 B：持久化账号筛选

1. 新增查询批次/明细实体、索引和迁移。
2. 新增创建批次、分页读批次、分页读明细、删除、重试 API。
3. 实时查询也先写 batch/item，再执行并更新结果。
4. 页面从后端读取当前和历史结果，不再依赖 Vue 内存状态。

### 阶段 C：真正后台任务

1. 在 `BatchTaskTypes` 增加账号筛选任务类型。
2. 在内置 Task Catalog 注册标准创建入口。
3. 实现唯一 batch handler，使用现有任务心跳/暂停/恢复合同。
4. Task Center 增加专用配置表单和中文详情。
5. 验证关闭浏览器和重启容器后能继续。

### 阶段 D：批量建群邀请并活跃

1. 先写配置 schema、状态机、客户租约/幂等设计。
2. 分阶段实现建群、邀请、活跃、标记已沟通。
3. 加模拟 Telegram service 的单元/集成测试，避免测试账号直接承担开发调试。
4. 小规模真实账号验收后再部署。

### 阶段 E：交付与部署

1. 前端：`pnpm --dir frontend run build`、`pnpm --dir frontend test`。
2. 后端：使用本机 .NET 8 SDK 构建并跑 531+ 测试。
3. 新 SQLite 数据库跑全量 Up，生产数据库副本跑升级；再验证 Down/Up。
4. 更新 `docs/developer/custom-change-log.md` 和客户管理指南。
5. commit/push。
6. 停 5000 容器、备份 `/opt/Telegram-Panel/docker-data`、立即重启旧容器；镜像构建成功后再切换。
7. 验证 5000 health、客户列表、账号筛选、任务中心和数据库迁移；确认 7000 未变。

## 6. 重点代码位置

- 客户列表：`frontend/src/views/Customers.vue`
- 账号筛选：`frontend/src/views/CustomerLookup.vue`
- 客户分类：`frontend/src/views/CustomerCategories.vue`
- 路由：`frontend/src/router/index.ts`
- 导航：`frontend/src/layouts/MainLayout.vue`
- API Client：`frontend/src/api/client.ts`
- API 类型：`frontend/src/api/types.ts`
- Panel API：`frontend/src/api/panel.ts`
- 客户 API：`src/TelegramPanel.Web/Api/CustomerManagementApi.cs`
- Telegram 查询：`src/TelegramPanel.Core/Services/Telegram/AccountTelegramToolsService.cs`
- 客户实体：`src/TelegramPanel.Data/Entities/Customer.cs`
- DbContext：`src/TelegramPanel.Data/AppDbContext.cs`
- 当前客户资料迁移：`src/TelegramPanel.Data/Migrations/20260915150000_AddCustomerTelegramProfile.cs`
- 任务类型：`src/TelegramPanel.Core/BatchTasks/BatchTaskTypes.cs`
- 任务目录：`src/TelegramPanel.Web/Modules/BuiltIn/TaskCatalogModule.cs`
- 后台任务：`src/TelegramPanel.Web/Services/BatchTaskBackgroundService.cs`
- 任务页面：`frontend/src/views/Tasks.vue`
- 客户前端测试：`frontend/tests/customerManagement.test.mjs`
- 项目变更记录：`docs/developer/custom-change-log.md`
- 客户指南：`docs/guides/customer-management.md`
- 外部服务器部署文档：`E:\Desktop\chongzhi\telegram_tool\docs\telegram-panel-server-deployment.md`

## 7. 已踩过的坑

1. **不要把前端循环叫后台任务。** 关闭页面必然中断，也没有任务中心状态、恢复点和可靠心跳。
2. **不要用临时联系人名称覆盖真实资料。** 手机号查找会导入临时联系人，`Lookup Contact` / `Telegram Lookup` 可能被 Telegram 原样回显。
3. **不要只过滤新数据而不清理旧脏数据。** 现有数据库已经有 `Lookup Contact`，需显式迁移或修复脚本。
4. **不要用一个 `Promise.all` 加载整页全部数据。** 任一元数据接口报错会导致整个客户页白屏/502观感；应独立加载并保留成功数据。
5. **不要只靠重试掩盖 502。** 还要查容器日志、反向代理超时、接口异常、SQLite 锁和 Telegram 调用耗时；GET 可有限重试，写操作不能无幂等地自动重放。
6. **Element Plus 固定列要复用账号列表验证过的背景/z-index/hover 样式。** 只加 `fixed="right"` 不够，宽表悬停时会被普通单元格覆盖。
7. **迁移必须加 `[DbContext]` 和 `[Migration]` 属性。** 手工迁移缺属性时 EF 可能不识别。
8. **SQLite 的 `DropColumnOperation` 在 EF 测试中报不支持。** 已改为迁移中的原生 `ALTER TABLE ... DROP COLUMN`；新增迁移同样需要实际做 Up/Down 测试。
9. **PowerShell 会提前展开远端 Bash 的 `$(date ...)`。** 上次因此生成 `/root/telegram-panel-data-.tar.gz`。后续使用上传的 shell 脚本、单引号严格转义或 Paramiko exec，且检查生成文件名包含时间戳。
10. **远端部署不要影响 7000 作者版。** compose 操作精确指定 `telegram-panel` 服务，部署后分别验证 5000 和 7000。
11. **不要覆盖用户已有改动或做破坏性 reset。** 本地先看 `git status`；服务器优先 `fetch + checkout + merge --ff-only`。
12. **账号删除含义必须写清。** 查询页“移除执行账号”和系统“删除账号”是两个不同动作。
13. **64 位 Telegram ID 在前端要避免 JS 精度损失。** 已有授权 hash 的相关处理经验；新增结果持久化 DTO 需检查 long/string 序列化。
14. **生日等 full-user 字段查询是额外 Telegram RPC。** 单项失败不应让基础用户查询整体失败，但需要在字段来源/完整性上留下标记。

## 8. 验收标准

下一轮完成后至少验证：

- 客户列表操作栏正常收缩，有全选本页；悬停、横向滚动、缩放时操作列始终可见。
- 旧 `Lookup Contact` 被清除；再次查询能用本次结果覆盖客户资料。
- 姓名、昵称、最后数据同步字段定义明确且前后端一致。
- 执行账号支持多选并展示昵称、状态、最后数据同步；移除操作语义明确。
- 查询结果换页面、刷新、重新登录后仍存在。
- “发送任务”在任务中心真实可见，浏览器关闭及容器重启后任务可恢复。
- 任务进度、心跳、错误分类、重试和延迟均由服务端维护。
- 前后端全量测试通过，新迁移在空库和生产副本均验证。
- 5000 切换到新镜像并 healthy，7000 作者版状态不变。

## 9. 工作区状态

创建本文前仓库 `git status --short` 为空，最新提交为 `6f95074`。本文是本轮唯一新增/修改文件，按用户要求用于下一次对话检索和续作。
