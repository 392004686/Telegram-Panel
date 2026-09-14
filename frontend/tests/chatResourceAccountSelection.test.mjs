import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const source = await readFile(new URL('../src/views/ChatResources.vue', import.meta.url), 'utf8')
const users = await readFile(new URL('../src/views/Users.vue', import.meta.url), 'utf8')
const layout = await readFile(new URL('../src/layouts/MainLayout.vue', import.meta.url), 'utf8')

test('单次发送执行账号显示身份状态并禁用不可用账号', () => {
  assert.match(source, /membershipAccountLabel\(account\)/)
  assert.match(source, /:disabled="!isMembershipAccountUsable\(account\)"/)
  assert.match(source, /自动选择（优先创建者，其次可用管理员）/)
  assert.match(source, /失效、冻结或受限账号不会参与执行/)
})

test('邀请成员接受手机号并说明分组账号不在群组时跳过', () => {
  assert.match(source, /国际格式手机号/)
  assert.match(source, /轮询账号若不在当前群组\/频道中，本次邀请会跳过/)
})

test('团队权限可配置左侧导航入口', () => {
  assert.match(users, /左侧导航栏功能/)
  assert.match(users, /editForm\.navigationItems/)
  assert.match(layout, /auth\.me\?\.navigationItems/)
})
