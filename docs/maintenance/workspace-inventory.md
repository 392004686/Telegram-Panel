# Workspace inventory

更新时间：2026-09-22

## `work/` 历史目录

| 目录 | 文件数 | 初步判断 | 处理策略 |
| --- | ---: | --- | --- |
| `basic-group-fix` | 1 | 群组修复临时副本 | 先保留，读取内容后归档 |
| `create-group-fix` | 4 | 创建群组相关临时副本 | 与主线差异比对后归档 |
| `dissolve-dm-update` | 2 | 私聊解散更新副本 | 保留证据，不直接合并 |
| `interactive-update` | 2 | 交互更新副本 | 保留证据，不直接合并 |
| `menu-update` | 1 | 菜单更新副本 | 与当前菜单实现比对 |
| `rename-console` | 1 | 控制台重命名副本 | 与当前品牌/日志实现比对 |
| `supergroup-params` | 4 | 超级群参数副本 | 与群组服务比对 |
| `Telegram-Panel` | 14935 | 当前主仓库，含构建/依赖缓存 | 仅保留源代码和有效文档 |

## 工作区缓存与运行产物

根目录包含 `__pycache__`、`artifacts`、`chrome-qa-profile`、`qa-node`、`runtime_sessions`、`session_data`、多张 QA 截图，以及主仓库中的 `bin`、`obj`、`node_modules`、浏览器缓存和运行数据。这些暂不删除，待确认用途并生成哈希清单后处理。

## 文档归一规则

1. 当前有效部署、配置、架构、模块和故障排查文档进入 `docs/` 唯一索引。
2. 历史交接记录保留在 `HANDOFF.md`，不复制成多个版本。
3. 临时输出、截图、构建日志和缓存不进入文档索引。
4. 删除前必须列出路径、类型、大小、来源和可恢复方式。

## 下一步盘点

- 对 7 个历史副本做文件内容哈希，并与 `Telegram-Panel` 主线做差异摘要。
- 扫描 `docs/` 内重复主题和过期部署提交，建立迁移表。
- 将仍有效文档链接收敛到 `docs/README.md`。
