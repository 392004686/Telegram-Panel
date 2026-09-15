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

test('查找用户已迁移到客户列表并保存查询结果', () => {
  const customers = read('src/views/Customers.vue')
  assert.match(customers, /查找用户/)
  assert.match(customers, /lookupNewCustomer/)
  assert.match(customers, /查看详情/)
  assert.doesNotMatch(read('src/views/Accounts.vue'), /手动查找用户是否存在/)
})

test('客户列表提供账号式筛选选择和批量管理', () => {
  const view = read('src/views/Customers.vue')
  assert.match(view, /type="selection"/)
  assert.match(view, /批量修改分类/)
  assert.match(view, /批量删除/)
  assert.match(read('src/views/CustomerCategories.vue'), /编辑客户分类/)
})
