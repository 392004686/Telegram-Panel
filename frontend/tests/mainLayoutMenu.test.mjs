import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const sourceUrl = new URL('../src/layouts/MainLayout.vue', import.meta.url)
const source = await readFile(sourceUrl, 'utf8')

test('侧栏子菜单默认全部收起', () => {
  assert.match(source, /const defaultOpeneds: string\[\] = \[\]/)
  assert.equal(source.match(/:default-openeds="defaultOpeneds"/g)?.length, 2)
})

test('选中子菜单时父级目录保持高对比可见', async () => {
  const styleSource = await readFile(new URL('../src/styles/global.css', import.meta.url), 'utf8')
  assert.match(styleSource, /--tp-menu-parent-active: #1f6eea/)
  assert.match(styleSource, /\.el-sub-menu\.is-active > \.el-sub-menu__title \{[\s\S]*color: var\(--tp-menu-parent-active\) !important/)
  assert.doesNotMatch(styleSource, /\.el-menu-item\.is-active span,\s*\.menu \.el-sub-menu\.is-active/)
})

test('Telegram API 回到系统设置且设备指纹保留独立入口', async () => {
  const routerSource = await readFile(new URL('../src/router/index.ts', import.meta.url), 'utf8')
  const settingsSource = await readFile(new URL('../src/views/Settings.vue', import.meta.url), 'utf8')
  assert.doesNotMatch(source, /label: 'Telegram API'/)
  assert.match(source, /label: '设备指纹'/)
  assert.match(routerSource, /path: 'telegram-api', redirect: '\/settings'/)
  assert.match(settingsSource, /<template #header>Telegram API<\/template>/)
})

test('魔改版顶部隐藏作者版本、更新入口和仓库链接', () => {
  assert.doesNotMatch(source, /openVersionDialog|versionDialog|version-chip/)
  assert.doesNotMatch(source, /github\.com\/moeacgx\/Telegram-Panel|title="GitHub"/)
  assert.doesNotMatch(source, /一键更新并重启|发现新版本/)
})

test('只读账号首次登录页面保留改密并隐藏改用户名入口', async () => {
  const passwordSource = await readFile(new URL('../src/views/AdminPassword.vue', import.meta.url), 'utf8')
  assert.match(passwordSource, /<template v-if="!auth\.isReadOnly">[\s\S]*保存用户名[\s\S]*<\/template>/)
  assert.match(passwordSource, /@click="savePassword">保存密码<\/el-button>/)
})
