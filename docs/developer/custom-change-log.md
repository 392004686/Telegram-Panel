# 自定义版本变更追踪

本文记录本仓库相对作者上游版本的自定义修改。以后每次功能或 UI 调整都应：

1. 独立提交，提交信息说明功能与影响范围；
2. 在本文追加记录，包括修改文件、验证结果和部署镜像；
3. 更新相对上游基线的补丁文件；
4. 合并作者新版时，以本文的“重点冲突区域”作为回归检查清单。

## 仓库与基线

- 作者仓库：`upstream = https://github.com/moeacgx/Telegram-Panel.git`
- 自有仓库：`origin = https://github.com/392004686/Telegram-Panel.git`
- 自定义分支：`codex/multi-user-ui`
- 当前上游基线：`f49d5f5`
- 当前状态：自定义分支相对 `upstream/main` 领先 2 个功能提交，上游无新增提交待合并。

## 变更记录

### `bd9386f` 多用户权限与运营工作台 UI

- 新增多用户后台凭据、角色及权限校验；旧单管理员凭据迁移到 v2 格式。
- 新增用户管理页面、路由、API 类型与请求封装。
- 调整登录页、仪表盘、主布局和全局样式。
- 更新权限、API 与 Docker 更新说明。
- 验证：前端、后端角色权限测试及 Docker 部署验证通过。

重点冲突区域：

- `src/TelegramPanel.Web/Services/AdminCredentialStore.cs`
- `src/TelegramPanel.Web/Services/PanelPermissionGuard.cs`
- `src/TelegramPanel.Web/Api/PanelAdminApiEndpoints.cs`
- `src/TelegramPanel.Web/Program.cs`
- `frontend/src/layouts/MainLayout.vue`
- `frontend/src/router/index.ts`
- `frontend/src/stores/auth.ts`
- `frontend/src/views/Dashboard.vue`
- `frontend/src/views/Login.vue`
- `frontend/src/views/Users.vue`
- `frontend/src/styles/global.css`

### `af3b811` 子菜单激活时父级导航对比度

- 父级目录激活色改为蓝色文字与浅蓝背景，避免白底白字。
- 深色模式增加对应父级激活颜色。
- 新增父级菜单高对比度回归测试。
- 修改文件：
  - `frontend/src/styles/global.css`
  - `frontend/tests/mainLayoutMenu.test.mjs`
- 验证：前端测试 `97/97`；构建通过；线上父级颜色为 `#1f6eea`，背景为 `#f0f5ff`。
- 已部署镜像：`telegram-panel:multi-user-ui-20260913`，镜像 ID `sha256:f815d493ff7cea1005351a46670704a0ca2f0831cd22235cd1aa1e83b44158ac`。

### `23047ec` 只读账号首次改密与定制版标识隐藏

- 允许只读审计账号调用 `POST /api/panel/settings/password` 修改自己的密码，使初次登录强制改密流程能够完成；其他非读取请求仍保持禁止。
- 只读账号的密码安全页隐藏“修改用户名”区域，仅保留首次改密需要的表单。
- Vue 主布局隐藏左上角版本号、新版本提示、版本弹窗、一键更新入口及作者仓库链接。
- 旧版 Blazor 主布局同步隐藏版本、新版本提示和作者仓库入口，避免从兼容页面再次出现。
- 作者更新检测与应用服务代码暂时保留，但当前 UI 不再提供入口；服务器继续使用 `TP_UPDATE_MODE=image` 和人工合并、构建、部署流程。
- 修改文件：
  - `src/TelegramPanel.Web/Services/PanelPermissionGuard.cs`
  - `src/TelegramPanel.Web/Components/Layout/MainLayout.razor`
  - `frontend/src/layouts/MainLayout.vue`
  - `frontend/src/views/AdminPassword.vue`
  - `tests/TelegramPanel.Web.Tests/PanelRolesTests.cs`
  - `frontend/tests/mainLayoutMenu.test.mjs`
  - `AGENTS.md`
- 验证：前端测试 `99/99`；前端生产构建通过；后端新增“只读可改自己的密码”和“其他写操作仍拒绝”回归用例。
- 已部署镜像：`telegram-panel:multi-user-ui-23047ec`，镜像 ID `sha256:5b1d494c478951fee4d3a9a0e7ddb5c75dcbddae1db9e7ef394ae7d59b89a673`。
- 线上验收：容器 `running/healthy`；`/ui/` 与 `/api/panel/auth/me` 均返回 `200`；编译产物不存在作者仓库、版本弹窗及一键更新入口标记；部署前后 `admin_auth.json` SHA-256 一致。

### 2026-09-13 作者版对比容器

- 在服务器新增作者原版对比实例，容器名 `telegram-panel-author`，镜像 `ghcr.io/moeacgx/telegram-panel:latest`。
- 宿主端口 `7000` 映射容器端口 `5000`；编排与独立数据目录位于 `/opt/Telegram-Panel-author/`。
- 数据库、Session、后台凭据、配置、Docker 网络均与 `5000` 端口的定制版隔离，不共享任何业务数据。
- 作者版设置为 `SelfUpdate__Mode=image`，更新通过该目录中的 `docker compose pull && docker compose up -d` 完成。
- 验证：作者版及定制版同时为 `running/healthy`；作者版 `/ui/` 与 `/api/panel/auth/me` 均返回 `200`。
- 停用：`cd /opt/Telegram-Panel-author && docker compose stop`；彻底移除容器但保留数据：`docker compose down`。

### 2026-09-13 Telegram X 品牌与详情单次发送

- 左上角、移动端、登录页及浏览器标题由 `Telegram Panel` 调整为 `Telegram X`。
- 群组与频道详情新增“单次立即发送”，支持文字、单张图片和单个视频，可自动选择或指定关联账号。
- 新增 `POST /api/panel/channels/{id}/message` 与 `POST /api/panel/groups/{id}/message`；上传内容只用于本次 Telegram 发送，不写入素材字典或数据库。
- 限制：文字 4096 字符、媒体说明 1024 字符、图片 20 MB、视频 200 MB；视频支持 MP4/MOV/M4V/WEBM/MKV。
- 权限：管理员和运营员可发送，只读账号只可查看；无数据库迁移。
- 重点冲突区域：`frontend/src/views/ChatResources.vue`、`frontend/src/layouts/MainLayout.vue`、`frontend/src/views/Login.vue`、`src/TelegramPanel.Web/Api/PanelAdminApiEndpoints.cs`、`src/TelegramPanel.Core/Services/Telegram/AccountTelegramToolsService.cs`。
- 验收：前端测试 `101/101`、生产构建、后端 Release 构建（0 警告/0 错误）及端点元数据测试 `4/4` 通过。`e1f9db5` 已部署为 `telegram-panel:multi-user-ui-e1f9db5`（镜像 ID `sha256:4e55a007d42c3cb789b450b64380c739cfaf6829dca8333092ad87ac7e939d83`）；容器 `running/healthy`，`/ui/` 与 `/api/panel/auth/me` 返回 200，编译资源包含 `Telegram X` 和“单次立即发送”，部署前后凭据文件 SHA-256 一致；作者版 7000 端口仍为 `running/healthy`。
- 回滚：切回上一镜像会移除品牌和发送入口，但不会撤回已经发到 Telegram 的消息。

### 2026-09-13 混合发送、导入指纹与视频字典

- 单次立即发送改为文字、图片、视频混合编辑；最多 10 个媒体。勾选“合并发送”时媒体作为同一个 Telegram 媒体组且文字作为说明，不勾选时按文字和文件顺序逐条发送。
- 账号列表固定操作列增加独立背景与层级，悬停任意行时操作按钮不再被滚动内容遮挡。
- Zip 导入增加“默认配置文件指纹（无配置时随机）”：逐账号读取 JSON 的 `app_version`、`device_model`、`system_version` 和语言字段并编码保存到账号 `DeviceProfileKey`；缺失字段稳定随机补齐，不新增数据库列。
- 数据字典补充填写说明并新增视频字典；视频文件持久化到 `/data/uploads/dictionaries/`。组合字典暂不新增：后续自动任务由一条消息模板分别引用文本、图片或视频字典，保持各素材库可复用且避免组合重复。
- 新增 `POST /api/panel/data-dictionaries/video`，支持 MP4/MOV/M4V/WEBM/MKV，单文件最大 200 MB。
- 验收：前端测试 `104/104`、生产构建、后端 Release 构建（0 警告/0 错误）及相关后端测试 `10/10` 通过。已部署 `telegram-panel:multi-user-ui-85408c8`（镜像 ID `sha256:0bdb28f05dfcc98773d9ea50d41e17502c97269093e5b3beafc848dad5bdf86d`）；5000 端口容器 `running/healthy`，UI 和认证接口为 200，三项新 UI 标记均存在，凭据哈希不变，7000 端口作者版保持健康。
- 重点冲突区域：`ChatResources.vue`、`Accounts.vue`、`AccountImport.vue`、`DataDictionaries.vue`、`PanelAdminApiEndpoints.cs`、`AccountImportService.cs`、`TelegramDeviceProfileCatalog.cs`、数据字典服务和资产存储服务。
- 回滚：切回上一镜像不会删除已保存字典或素材；旧版会忽略视频字典和编码的导入画像。回滚前如需旧版继续连接这些账号，应在账号详情改为旧版可识别的内置画像。

### 2026-09-14 修复账号列表固定列穿透

- 状态：**已部署**（`telegram-panel:multi-user-ui-d57c57e`）。在表格内部增加与操作列等宽的不透明遮罩，并把固定列提升到遮罩上方；移动端遮罩宽度同步为 70px。
- 复现位置：工作台 → 账号列表；在桌面宽屏和出现横向滚动条的布局均可出现。
- 复现步骤：进入账号列表，将底部横向滚动条向右拖动或把鼠标移到账号行；“Telegram 状态/日期”等非固定列内容会进入右侧固定“操作”列区域，覆盖刷新图标和更多操作按钮。截图中可看到日期文本与刷新图标重叠。
- 预期：固定操作列始终使用不透明背景并裁剪其左侧滚动内容；滚动、悬停及表格阴影变化时，三个操作按钮都保持完整可见和可点击。
- 实现位置：`frontend/src/views/Accounts.vue` 的 `.el-table__inner-wrapper::after` 负责遮住滚动内容，`.el-table-fixed-column--right` 保持在遮罩上方；不再只依赖单元格背景。
- 验收矩阵：浏览器缩放 80%/100%/125%，窗口宽度 1280/1600/1920；分别在首列、横向滚动中间、最右端悬停每一行，确认滚动列文字不进入操作列，按钮不截断。
- 涉及文件：`frontend/src/views/Accounts.vue`、`frontend/src/styles/global.css`；修复时补充可渲染的浏览器回归或截图验证，不能只做源码正则测试。

### 2026-09-14 兼容 Zip JSON 指纹别名

- 状态：**已部署**（`telegram-panel:multi-user-ui-d57c57e`）。新增 `device` → `device_model`、`sdk` → `system_version` 别名，并保留原字段及驼峰字段。
- 示例预期结果：`app_version=5.15.0 x64`、`device_model=ASUS ExpertBook B9`、`system_version=Windows 10`、`system_lang_code=en-us`、`lang_code=en`。
- `lang_pack=tdesktop` 和 `system_lang_pack=en-us` 不直接映射到 WTelegram 的五个设备画像字段，避免把语言包标识误当成语言代码；原值仍留在导入源 JSON，不需要写入账号画像。
- 验收用例：用包含 `app_id`、`app_hash`、`device`、`sdk` 的示例 JSON 调用 `BuildImportedProfileKey`，再解码并断言五项值完全等于上述预期；同时保留字段缺失时的稳定随机补齐测试。
- 涉及文件：`src/TelegramPanel.Core/Services/Telegram/TelegramDeviceProfileCatalog.cs`、`tests/TelegramPanel.Web.Tests/TelegramDeviceProfileCatalogTests.cs`、`docs/guides/account-import.md`。

### 2026-09-14 账号详情保留导入指纹

- 状态：**已部署**（`telegram-panel:multi-user-ui-d57c57e`）。编码画像回显“导入配置文件指纹（来自 JSON）”并展示五项摘要；其他账号显示禁用说明。
- 当前代码仅在 `DeviceProfileKey` 已是 `imported-json:<编码内容>` 时动态插入“导入配置文件指纹”选项；线上截图仍回显“Windows 默认指纹”，需要同时核查该账号是否在功能部署前导入、导入请求是否实际提交 `imported-json`、服务端是否保存编码值，以及详情 DTO 是否原样返回。
- 防误改：仅当用户主动触发设备指纹下拉框 `change` 时提交 `DeviceProfileKey`；编辑备注、二级密码等字段不会改写画像。服务端校验编码画像时保留 Base64URL 原始大小写。
- 旧账号处理：若数据库中只有 `windows-default` 等内置 key，且原始导入 JSON 已不存在，则不能伪造恢复原画像；界面应明确显示当前实际画像。重新导入相同账号并选择配置文件指纹后才保存 JSON 画像。
- 验收：导入示例 JSON 后打开详情显示“导入配置文件指纹”，仅修改备注并保存后编码 key 完全不变；主动切换内置画像才改变 key；刷新和重新登录后仍正确回显。
- 涉及文件：`frontend/src/views/Accounts.vue`、账号详情更新 DTO/API、`TelegramDeviceProfileCatalog.cs` 及对应测试。

### 2026-09-14 修复新建任务表单文字遮挡

- 状态：**已部署**（`telegram-panel:multi-user-ui-d57c57e`）。帮助文字独占整行并自然撑高，移除负上边距；动态表单字段统一下间距，内容允许换行，弹窗隐藏横向溢出。
- 预期：每个字段形成独立纵向区块，标签、控件、帮助文字按顺序排列；帮助文字允许自动换行并撑高容器，不能使用覆盖后续控件的固定高度或负偏移。窄屏和长中文说明下也不得重叠。
- 下次排查重点：任务动态配置表单的 `.el-form-item` 下边距、帮助文本 `line-height/white-space/position`、嵌套行列布局固定高度，以及全局 `.form-hint`/`.cell-sub` 样式；优先统一组件级布局，不逐字段添加临时 margin。
- 验收矩阵：账号持续活跃及其他所有内置任务类型；窗口宽度 375/768/1280/1920，浏览器缩放 80%/100%/125%；逐项截图确认标签、输入框、帮助文字和按钮不交叠，文本完整可读，页面高度随内容增长。
- 涉及文件：`frontend/src/views/Tasks.vue`、任务配置子组件及 `frontend/src/styles/global.css`；需要浏览器实际渲染截图验证，源码正则测试不作为完成依据。

### 2026-09-14 本批次验证记录

- 前端：107/107 测试通过；Vue TypeScript 检查及 Vite 生产构建通过。
- 后端：Docker .NET 8 环境编译通过；设备画像目录测试 7/7 通过。
- 文档：MkDocs strict 构建通过。
- 部署：5000 端口镜像 `telegram-panel:multi-user-ui-d57c57e`，镜像 ID `sha256:4e0f99b82233c907cbc4922961ca99da08ae1b1ca0e9092c72abed1a3afaa43f`，HTTP 302（跳转登录）正常；7000 端口作者版 HTTP 302 正常。
- 回滚：实际切回 `telegram-panel:multi-user-ui-85408c8` 并验证 HTTP 302，随后恢复新镜像并再次验证 HTTP 302。
- 浏览器视觉验收：自动化浏览器连接该 HTTP 页面超时；源码、组件回归及生产构建已完成，固定列在不同缩放比例下仍需人工页面复核。

## 上游升级检查清单

```bash
git fetch upstream --prune
git switch codex/multi-user-ui
git switch -c upgrade/<目标版本>
git merge upstream/main
```

合并后必须检查：

- `git diff --name-only <旧上游基线>..upstream/main` 是否覆盖上述重点冲突区域；
- 登录、当前用户、用户管理、三种角色权限及未授权 API；
- 主布局、菜单展开/选中状态、浅色与深色主题；
- 前端测试及构建、后端测试；
- 使用新镜像灰度启动，保留旧镜像和数据备份后再正式切换。

升级完成后，把“当前上游基线”更新为实际合并的提交，并追加升级提交、冲突文件、处理方式、测试输出和部署镜像 ID。

### 2026-09-14 群组详情、执行账号与成员导航权限

- 修复群组受限账号读取管理员时 `FROZEN_METHOD_INVALID` 冒泡为 HTTP 500，再被异常处理器包装成 404；受限时详情继续展示，管理员区域返回空并给出明确警告。
- 单次立即发送执行账号按 `手机号(昵称)@用户名` 展示，并显示 Telegram 状态；停用、失效、冻结和受限账号在选择框中禁用。自动选择定义为：仅在该群组/频道已同步账号中优先创建者，其次可用管理员。
- 群组/频道数据库记录仍按 Telegram ID 合并为一个资源，多账号归属保存在关联表并在“本系统账号”列表逐项展示。
- 邀请成员支持 `@username`、username 和国际格式手机号；手机号通过 Telegram 联系人导入解析目标。账号分组轮询时，若轮到的账号不属于目标群组/频道，则跳过该项并写入任务失败明细。
- 已完成/失败/取消任务的运行阶段按最终状态归一化，避免已完成任务仍显示 `running`。
- 团队与权限编辑框增加左侧导航栏功能勾选；角色继续控制 API 操作权限，导航勾选仅控制登录后的入口显示。
- Telegram MTProto 没有可直接读取账号“双方 Spam 状态”的官方方法；现有状态检测继续以会话、创建频道探测及实际 RPC 错误为依据。

#### 本批次部署验证

- 提交：`61b3fa3`；镜像：`telegram-panel:multi-user-ui-61b3fa3`；镜像 ID：`sha256:f45ecb695c591749d8c02cec2206c0177974d50d81259fd0f6a9c0e4eb09125d`。
- 前端测试 110/110 通过，Vue TypeScript 与 Vite 生产构建通过；服务端 .NET 测试 531/531 通过；MkDocs strict 通过。
- 5000 端口定制版与 7000 端口作者版均返回正常登录跳转。已实际回滚至 `telegram-panel:multi-user-ui-d57c57e` 验证，再恢复新镜像。

### 2026-09-15 标准任务创建、客户管理与单用户查询

- 为频道邀请、群组邀请、账号加群/订阅、Bot 频道邀请以及两种 Bot 管理员任务补充标准创建编辑器，任务中心不再返回“未开放标准创建入口”；当前通用编辑器按原业务配置结构接收 JSON 与任务总数。
- 新增客户管理页及客户、客户分组、导入批次、分组关联和批次关联数据表；支持手机号或 `@username` 批量导入、去重、分组、批次留痕、筛选及删除。
- 账号列表的“加入的群组”弹窗新增单用户查询测试；用户名使用公开解析接口，手机号使用联系人导入接口，结果不自动写入客户库。
- 数据库迁移：`20260915090000_AddCustomerManagement`。回滚旧镜像会保留新表并由旧版忽略。
- 重点冲突区域：`AppDbContext.cs`、`PanelAdminApiEndpoints.cs`、`AccountTelegramToolsService.cs`、`TaskCatalogModule.cs`、`Accounts.vue`、`MainLayout.vue`、`Users.vue`。
- 本地验收：前端生产构建通过；前端测试 113/113；服务端测试 531/531；Release 编译 0 错误；MkDocs strict 通过。
- 迁移验收：从空 SQLite 顺序应用全部迁移到 `20260915090000_AddCustomerManagement`，确认生成 `Customers`、`CustomerGroups`、`CustomerGroupAssignments`、`CustomerImportBatches`、`CustomerBatchItems`。
- 部署验收（2026-09-15）：提交 `aeb683c` 已部署到 5000 端口，镜像 `telegram-panel:multi-user-ui-aeb683c`，镜像 ID `sha256:32dae79bd73b27ff95c74c848e75c3760fb81cd9398df9813ba998541e945ed5`。`/healthz`、`/ui/customers`、`/api/panel/auth/me` 均返回 HTTP 200；线上数据库已迁移至 `20260915090000_AddCustomerManagement` 并确认五张客户管理表存在。7000 端口作者版容器保持原镜像且健康。
- 部署前完整数据备份：`/root/telegram-panel-data-20260915-095330.tar.gz`，SHA-256 `4924c5b8c71cc0bb407075a80da3958dce0ead942014f5f73e41d54f50c6995b`。保留上一镜像 `telegram-panel:multi-user-ui-61b3fa3` 作为运行回滚点。

### 2026-09-15 客户管理界面对齐与查找入口迁移

- 客户管理改为与账号管理一致的父子导航：客户列表、客户分类。
- “手动查找用户是否存在”从账号加入群组弹窗移除，改为客户列表“查找用户”；支持选择可用执行账号，结果直接新增或更新客户档案。
- 客户列表补齐查询状态、客户分类、导入批次和关键字筛选，增加复选、批量修改分类、批量删除、单行查询和查看详情。
- 客户分类补齐新增、编辑、删除、描述和客户数量统计，不再只有导入时临时添加。
- 新增客户详情、直接查询、已有客户查询、批量操作和客户分类增删改 API；无数据库结构变更。
- 重点冲突区域：`Customers.vue`、`CustomerCategories.vue`、`MainLayout.vue`、`Users.vue`、`CustomerManagementApi.cs`。
- 回滚：切回镜像 `telegram-panel:multi-user-ui-aeb683c`；新增 API 和界面消失，已存在客户及分类数据保持不变。
- 部署验收：提交 `341390a` 已部署到 5000 端口，镜像 `telegram-panel:multi-user-ui-341390a`，镜像 ID `sha256:5a3c3076670a328bbbd9a779d89c539a4c40a155b2203c373c5819de4610a11a`；客户列表、客户分类页面 HTTP 200，相关 API 未登录时按预期 302 跳转登录，容器健康。7000 端口作者版未改动且健康。
- 部署前备份：`/root/telegram-panel-data-20260915-134207.tar.gz`，SHA-256 `cc5452bb59cbf553cb14f589456659cb46fb608781c5ecb59710cffe5990ae20`。

