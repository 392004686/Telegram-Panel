import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'

const form = readFileSync(new URL('../src/components/TaskConfigForm.vue', import.meta.url), 'utf8')
const tasks = readFileSync(new URL('../src/views/Tasks.vue', import.meta.url), 'utf8')

test('任务动态表单帮助文字独占整行并撑开字段高度', () => {
  assert.match(form, /\.form-hint\s*\{[\s\S]*?display: block;[\s\S]*?width: 100%;[\s\S]*?overflow-wrap: anywhere;/)
  assert.match(form, /\.task-config-form :deep\(\.el-form-item\)[\s\S]*?margin-bottom: 22px/)
  assert.match(form, /\.el-form-item__content[\s\S]*?flex-wrap: wrap/)
})

test('新建任务弹窗隐藏横向溢出且长说明自动换行', () => {
  assert.match(tasks, /task-dialog \.el-dialog__body[\s\S]*?overflow-x: hidden/)
  assert.match(tasks, /task-dialog \.el-alert__title[\s\S]*?white-space: normal/)
})

test('建群邀请任务支持强制二次确认和自定义图片倍率', () => {
  assert.match(form, /强制二次确认/)
  assert.match(form, /forceRecontact/)
  assert.match(form, /force_recontact/)
  assert.match(form, /图片倍率/)
  assert.match(form, /el-input-number v-model="rule.materialScale"/)
})
