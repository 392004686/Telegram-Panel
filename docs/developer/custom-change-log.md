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
