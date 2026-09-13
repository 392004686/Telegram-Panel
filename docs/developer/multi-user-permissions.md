# 后台多用户与权限模型

## 适用范围

本文适用于引入后台多用户权限后的版本。前置条件是 `AdminAuth:Enabled=true`，并且部署时持久化面板数据目录。用户入口位于 `/ui/users`，只有管理员可见。

## 角色合同

| 角色 | 权限标识 | 允许行为 | 明确限制 |
| --- | --- | --- | --- |
| `admin` 管理员 | `read, operate, admin` | 全部读写、成员管理、系统设置、模块与更新 | 最后一名启用中的管理员不得被删除、停用或降级 |
| `operator` 运营人员 | `read, operate` | 查看数据并执行常规账号、群组、频道和任务操作 | 不可管理成员、模块、外部 API、核心设置、系统操作、删除和清理 |
| `auditor` 审计员 | `read` | 使用 GET/HEAD 只读查看 | 所有数据修改请求返回 403 |

权限由服务端 `PanelPermissionGuard` 强制执行，前端隐藏按钮和菜单只是交互提示，不是安全边界。拒绝响应为 HTTP `403`，JSON 中包含 `code=PERMISSION_DENIED`。后台认证关闭时维持旧版无登录限制行为。

## 接口与身份

登录 Cookie 同时包含用户名和角色 claim。`GET /api/panel/auth/me` 新增：

```json
{
  "authenticated": true,
  "username": "operator-1",
  "role": "operator",
  "permissions": ["read", "operate"]
}
```

用户增删改、重置密码接口见[管理接口速查](../reference/api.md#后台成员与角色)。用户被停用、删除或角色发生变化后，旧 Cookie 会在后续请求时失效，要求重新登录。用户修改自己的用户名或密码仍需提交当前密码。

## 持久化与迁移

凭据继续保存在数据目录的 `admin_auth.json`，格式版本从单管理员结构升级为 `version: 2`，核心字段为 `users` 数组。启动读取旧文件时，原用户名、PBKDF2 salt/hash、迭代次数、创建时间和强制改密状态会原样迁移为首个 `admin` 用户，再以原子临时文件替换写回。迁移不重置密码。

新增或重置用户密码继续使用独立 salt 的 PBKDF2 哈希；响应不返回 salt、hash 或明文密码。必须持久化该文件所在目录，不能把它只留在容器可写层。

## 验收条件

1. 原管理员用原密码登录成功，`auth/me` 返回 `role=admin`。
2. 管理员可创建 operator/auditor，并可编辑状态、角色和重置密码。
3. operator 可读取 dashboard 并执行普通 POST；访问 `/api/panel/users` 或发送 DELETE 得到 403。
4. auditor 的 GET 成功，任意 POST 得到 403 和 `PERMISSION_DENIED`。
5. 尝试删除、停用或降级最后一名管理员得到 400，凭据文件保持有效。
6. 容器重启后所有成员仍存在，已停用用户的旧 Cookie 不再有效。

## 故障排查

- 登录后立刻变成未登录：检查成员是否被停用、改名或变更角色，并查看 Cookie 验证日志。
- 所有写请求都是 403：读取 `auth/me` 确认当前 `role` 与 `permissions`，不要只依赖前端显示。
- 迁移启动失败：停止容器，检查 `admin_auth.json` 是否为完整 JSON、目录是否可写，再从部署备份恢复。
- 重启后用户丢失：检查数据目录挂载以及 `AdminAuth:CredentialsPath` 的实际解析路径。

## 回滚

回滚前停止容器并备份当前 `admin_auth.json`。恢复旧程序版本时，应同时恢复部署前保存的旧格式凭据文件；不要把 v2 文件交给不了解 `users` 数组的旧版本。重新启动后以原管理员登录，并验证 `GET /api/panel/auth/me`。若只回滚界面而保留新后端，可继续使用 v2 文件。
