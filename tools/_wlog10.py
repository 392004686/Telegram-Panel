from pathlib import Path
p=Path('frontend/src/views/Tasks.vue')
t=p.read_text(encoding='utf-8')
old='''const detailDialog = ref({
  visible: false,
  title: '',
  content: '',
})'''
new='''const detailDialog = ref({
  visible: false,
  title: '',
  content: '',
  taskId: 0,
  logs: [] as Array<{ id: number; level: string; message: string; createdAt: string }>,
  logTotal: 0,
  logPage: 1,
  logPageSize: 20,
  canExport: false,
})'''
if old not in t: raise SystemExit('detail ref missing')
t=t.replace(old,new,1)
old='''  detailDialog.value = {
    visible: true,
    title: `任务详情 #${task.id}`,
    content: lines.join('\n'),
  }
}'''
new='''  detailDialog.value = {
    visible: true,
    title: `任务详情 #${task.id}`,
    content: lines.join('\n'),
    taskId: task.id,
    logs: [],
    logTotal: 0,
    logPage: 1,
    logPageSize: 20,
    canExport: displayStatus(task) === 'completed' || displayStatus(task) === 'failed',
  }
  await loadTaskLogs()
}

async function loadTaskLogs() {
  if (!detailDialog.value.taskId) return
  const r = await panelApi.taskLogs(detailDialog.value.taskId, { page: detailDialog.value.logPage, pageSize: detailDialog.value.logPageSize })
  detailDialog.value.logs = r.items
  detailDialog.value.logTotal = r.total
}

async function exportTaskLogs() {
  if (!detailDialog.value.taskId) return
  const rows = [['time','level','message']]
  let page = 1
  const pageSize = 200
  while (true) {
    const r = await panelApi.taskLogs(detailDialog.value.taskId, { page, pageSize })
    for (const item of r.items) rows.push([formatTime(item.createdAt), item.level, item.message.replaceAll('"','""')])
    if (page * pageSize >= r.total) break
    page += 1
  }
  const csv = rows.map((x) => x.map((c) => `"${c}"`).join(',')).join('\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `task-${detailDialog.value.taskId}-logs.csv`
  a.click()
  URL.revokeObjectURL(url)
}'''
if old not in t: raise SystemExit('assign missing')
t=t.replace(old,new,1)
if '.log-toolbar' not in t:
    t=t.replace('<style scoped>', '<style scoped>\n.log-toolbar{display:flex;align-items:center;justify-content:space-between;margin:16px 0 8px}', 1)
p.write_text(t, encoding='utf-8')
print('tasks logs ok')
