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

## 上游升级检查清单

### 2026-09-13 混合发送、导入指纹与视频字典

- 单次立即发送改为文字、图片、视频混合编辑；最多 10 个媒体。勾选“合并发送”时媒体作为同一个 Telegram 媒体组且文字作为说明，不勾选时按文字和文件顺序逐条发送。
- 账号列表固定操作列增加独立背景与层级，悬停任意行时操作按钮不再被滚动内容遮挡。
- Zip 导入增加“默认配置文件指纹（无配置时随机）”：逐账号读取 JSON 的 `app_version`、`device_model`、`system_version` 和语言字段并编码保存到账号 `DeviceProfileKey`；缺失字段稳定随机补齐，不新增数据库列。
- 数据字典补充填写说明并新增视频字典；视频文件持久化到 `/data/uploads/dictionaries/`。组合字典暂不新增：后续自动任务由一条消息模板分别引用文本、图片或视频字典，保持各素材库可复用且避免组合重复。
- 新增 `POST /api/panel/data-dictionaries/video`，支持 MP4/MOV/M4V/WEBM/MKV，单文件最大 200 MB。
- 验收：前端测试 `104/104`、生产构建、后端 Release 构建（0 警告/0 错误）及相关后端测试 `10/10` 通过。已部署 `telegram-panel:multi-user-ui-85408c8`（镜像 ID `sha256:0bdb28f05dfcc98773d9ea50d41e17502c97269093e5b3beafc848dad5bdf86d`）；5000 端口容器 `running/healthy`，UI 和认证接口为 200，三项新 UI 标记均存在，凭据哈希不变，7000 端口作者版保持健康。
- 重点冲突区域：`ChatResources.vue`、`Accounts.vue`、`AccountImport.vue`、`DataDictionaries.vue`、`PanelAdminApiEndpoints.cs`、`AccountImportService.cs`、`TelegramDeviceProfileCatalog.cs`、数据字典服务和资产存储服务。
- 回滚：切回上一镜像不会删除已保存字典或素材；旧版会忽略视频字典和编码的导入画像。回滚前如需旧版继续连接这些账号，应在账号详情改为旧版可识别的内置画像。

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

