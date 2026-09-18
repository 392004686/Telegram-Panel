import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'

const resources = readFileSync(new URL('../src/views/ChatResources.vue', import.meta.url), 'utf8')
const api = readFileSync(new URL('../src/api/panel.ts', import.meta.url), 'utf8')
const layout = readFileSync(new URL('../src/layouts/MainLayout.vue', import.meta.url), 'utf8')

test('群组和频道详情支持单次发送文字图片和视频', () => {
  assert.match(resources, /单次立即发送/)
  assert.match(resources, /选择图片或视频/)
  assert.match(resources, /合并发送（作为一条媒体组消息）/)
  assert.match(resources, /form\.append\('merge'/)
  assert.match(resources, /form\.append\('files'/)
  assert.match(resources, /panelApi\.sendChannelMessage/)
  assert.match(resources, /panelApi\.sendGroupMessage/)
  assert.match(api, /\/channels\/\$\{id\}\/message/)
  assert.match(api, /\/groups\/\$\{id\}\/message/)
})

test('主布局品牌显示 Telegram X', () => {
  assert.match(layout, />Telegram X</)
  assert.doesNotMatch(layout, />Telegram Panel</)
})

test('群组详情提供活跃消息规则添加器', () => {
  assert.match(resources, /活跃消息规则（测试发送）/)
  assert.match(resources, /按规则发送/)
  assert.match(resources, /sendGroupEngagementRules/)
  assert.match(api, /\/groups\/\$\{id\}\/engagement-rules/)
})
