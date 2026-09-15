import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'

const read = (path) => fs.readFileSync(new URL(`../${path}`, import.meta.url), 'utf8')

test('客户管理提供批次分组导入和状态筛选', () => {
  const view = read('src/views/Customers.vue')
  assert.match(view, /批量导入客户/)
  assert.match(view, /batchName/)
  assert.match(view, /groupId/)
  assert.match(view, /lookupStatus/)
})

test('客户管理已接入路由导航和团队导航权限', () => {
  assert.match(read('src/router/index.ts'), /path: 'customers'/)
  assert.match(read('src/layouts/MainLayout.vue'), /index: '\/customers', label: '客户管理'/)
  assert.match(read('src/views/Users.vue'), /value: '\/customers', label: '客户管理'/)
})

test('账号加入群组弹窗提供不入库的单用户查询测试', () => {
  const view = read('src/views/Accounts.vue')
  assert.match(view, /手动查找用户是否存在/)
  assert.match(view, /lookupAccountUser/)
  assert.match(view, /只做单次能力测试，不写入客户管理/)
})
