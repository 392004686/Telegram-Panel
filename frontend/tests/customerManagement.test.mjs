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
  assert.match(read('src/router/index.ts'), /path: 'customers\/categories'/)
  assert.match(read('src/layouts/MainLayout.vue'), /index: 'customers-group', label: '客户管理'/)
  assert.match(read('src/layouts/MainLayout.vue'), /index: '\/customers', label: '客户列表'/)
  assert.match(read('src/views/Users.vue'), /value: 'customers-group', label: '客户管理'/)
})

test('账号筛选已从客户列表拆分并支持批量详细查询', () => {
  const lookup = read('src/views/CustomerLookup.vue')
  assert.match(lookup, /账号分类轮询/)
  assert.match(lookup, /createCustomerLookupBatch/)
  assert.match(lookup, /历史查询批次/)
  assert.match(lookup, /全选本页/)
  assert.match(lookup, /最小间隔/)
  assert.match(lookup, /Premium/)
  assert.match(read('src/router/index.ts'), /customers\/lookup/)
  assert.match(read('src/views/Customers.vue'), /查看详情/)
  assert.doesNotMatch(read('src/views/Accounts.vue'), /手动查找用户是否存在/)
})

test('客户列表提供账号式筛选选择和批量管理', () => {
  const view = read('src/views/Customers.vue')
  assert.match(view, /type="selection"/)
  assert.match(view, /批量修改分类/)
  assert.match(view, /批量删除/)
  assert.match(view, /全选本页/)
  assert.match(view, /customers-table/)
  assert.match(read('src/views/CustomerCategories.vue'), /编辑客户分类/)
})

test('客户列表提供批量修改执行状态', () => {
  const view = read('src/views/Customers.vue')
  assert.match(view, /批量修改执行状态/)
  assert.match(view, /openBatchStatus/)
  assert.match(view, /set_interaction/)
})
