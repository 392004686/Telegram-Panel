# Telegram Panel 接口清单与映射进度

更新时间：2026-09-22

## 统计口径

按 `MapGet/MapPost/MapPut/MapPatch/MapDelete` 路由定义统计，当前约 **271 个 HTTP 接口**；其中 269 个是源码中的固定路径，少量由动态映射生成。该数字包含健康检查、认证、后台管理、模块接口，不等同于 Telegram RPC 方法数量。

## 分类总览

| 分类 | 数量 | 映射状态 | 映射路径 |
|---|---:|---|---|
| 账户、登录、设备、资料 | 49 | 进行中 | `modules/command-console/mappings/account/` |
| Bot 频道 | 27 | 未开始 | `modules/command-console/mappings/bot-channel/` |
| 系统设置 | 23 | 未开始 | `modules/command-console/mappings/settings/` |
| 群组 | 19 | 第一批 | `modules/command-console/mappings/group/` |
| 频道 | 18 | 未开始 | `modules/command-console/mappings/channel/` |
| 代理 | 15 | 第一批 | `modules/command-console/mappings/proxy/` |
| 普通任务 | 14 | 未开始 | `modules/command-console/mappings/task/` |
| 计划任务 | 7 | 未开始 | `modules/command-console/mappings/scheduled-task/` |
| 客户、客户查询 | 13 | 未开始 | `modules/command-console/mappings/customer/` |
| 数据字典 | 7 | 未开始 | `modules/command-console/mappings/dictionary/` |
| 机器人及 Bot 分类 | 10 | 未开始 | `modules/command-console/mappings/bot/` |
| 模块与外部 API | 11 | 未开始 | `modules/command-console/mappings/module/` |
| 分类、认证、导航、健康检查等 | 约 18 | 未开始 | `modules/command-console/mappings/system/` |

## 映射合同

每个新命令必须同步维护：

- `event-type-cn.json`：事件类型中文名
- `action-cn.json`：动作名中文名
- `field-cn.json`：返回字段中文名
- 分类目录下的路径映射文件：HTTP 方法、路径、命令名、参数、返回模板、状态

路径映射文件建议字段：`method`、`path`、`command`、`category`、`requestExample`、`responseFields`、`status`、`completedAt`。

状态只允许：`未开始`、`进行中`、`已完成`、`需复核`。完成一个接口后，必须在对应路径后标记 `已完成`，并记录验证日期。

## 官方返回模板

Telegram RPC 返回结构应以项目实际使用的 WTelegram/Telegram TL 类型和官方方法文档交叉核对；官方模板只作为字段参考，不能替代项目真实返回。原始 RPC 错误、错误码和未映射字段必须保留。
