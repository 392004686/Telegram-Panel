from pathlib import Path
p=Path('frontend/src/views/Tasks.vue')
t=p.read_text(encoding='utf-8')
start=t.find('  detailDialog.value = {')
end=t.find('async function loadTaskDetail', start)
if start<0 or end<0: raise SystemExit('markers')
block="  detailDialog.value = {\n    visible: true,\n    title: `任务详情 #${task.id}`,\n    content: lines.join('\\n'),\n    taskId: task.id,\n    logs: [],\n    logTotal: 0,\n    logPage: 1,\n    logPageSize: 20,\n    canExport: displayStatus(task) === 'completed' || displayStatus(task) === 'failed',\n  }\n  await loadTaskLogs()\n}\n\nasync function loadTaskLogs() {\n  if (!detailDialog.value.taskId) return\n  const r = await panelApi.taskLogs(detailDialog.value.taskId, { page: detailDialog.value.logPage, pageSize: detailDialog.value.logPageSize })\n  detailDialog.value.logs = r.items\n  detailDialog.value.logTotal = r.total\n}\n\nasync function exportTaskLogs() {\n  if (!detailDialog.value.taskId) return\n  const rows = [['time','level','message']]\n  let page = 1\n  const pageSize = 200\n  while (true) {\n    const r = await panelApi.taskLogs(detailDialog.value.taskId, { page, pageSize })\n    for (const item of r.items) rows.push([formatTime(item.createdAt), item.level, String(item.message || '').replaceAll('\"','\"\"')])\n    if (page * pageSize >= r.total) break\n    page += 1\n  }\n  const csv = rows.map((x) => x.map((c) => '\"' + c + '\"').join(',')).join('\\n')\n  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })\n  const url = URL.createObjectURL(blob)\n  const a = document.createElement('a')\n  a.href = url\n  a.download = `task-${detailDialog.value.taskId}-logs.csv`\n  a.click()\n  URL.revokeObjectURL(url)\n}\n\n"
t=t[:start]+block+t[end:]
if '.log-toolbar' not in t: t=t.replace('<style scoped>','<style scoped>\n.log-toolbar{display:flex;align-items:center;justify-content:space-between;margin:16px 0 8px}',1)
p.write_text(t, encoding='utf-8')
print('tasks splice ok')
