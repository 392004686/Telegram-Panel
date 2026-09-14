import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'

const dictionaries = readFileSync(new URL('../src/views/DataDictionaries.vue', import.meta.url), 'utf8')
const accountImport = readFileSync(new URL('../src/views/AccountImport.vue', import.meta.url), 'utf8')
const accounts = readFileSync(new URL('../src/views/Accounts.vue', import.meta.url), 'utf8')

test('数据字典解释填写和读取方式并提供视频字典', () => {
  assert.match(dictionaries, /文本字典一行一条/)
  assert.match(dictionaries, /新建视频字典/)
  assert.match(dictionaries, /saveVideoDictionary/)
  assert.match(dictionaries, /不需要复制一份组合字典/)
})

test('Zip 导入可复用配置文件设备指纹', () => {
  assert.match(accountImport, /默认配置文件指纹（无配置时随机）/)
  assert.match(accountImport, /app_version、device_model、system_version/)
})

test('账号列表固定操作列在悬停时保持可见', () => {
  assert.match(accounts, /class="accounts-table"/)
  assert.match(accounts, /el-table__body tr:hover > \.el-table-fixed-column--right/)
  assert.match(accounts, /el-table__inner-wrapper::after/)
  assert.match(accounts, /z-index: 5 !important/)
})

test('账号详情回显导入 JSON 指纹且无关保存不提交指纹字段', () => {
  assert.match(accounts, /导入配置文件指纹（来自 JSON）/)
  assert.match(accounts, /JSON 指纹：应用/)
  assert.match(accounts, /details\.deviceProfileChanged \? \{ deviceProfileKey:/)
})
