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
