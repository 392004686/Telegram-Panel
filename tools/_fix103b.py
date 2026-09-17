from pathlib import Path
p = Path(r'frontend/src/views/Tasks.vue')
t = p.read_text(encoding='utf-8')
old = "const detailDialog = ref({\n  visible: false,\n  title: '',\n  content: '',\n})"
new = "const detailDialog = ref({\n  visible: false,\n  title: '',\n  content: '',\n  taskId: 0,\n  logs: [] as Array<{ id: number; level: string; message: string; createdAt: string }>,\n  logTotal: 0,\n  logPage: 1,\n  logPageSize: 20,\n  canExport: false,\n})"
if old not in t: raise SystemExit('init missing')
t = t.replace(old, new, 1)
old2 = "    content: lines.join('\\n'),\n  }\n}\n\nfunction statusName"
new2 = "    content: lines.join('\\n'),\n    taskId: 0,\n    logs: [],\n    logTotal: 0,\n    logPage: 1,\n    logPageSize: 20,\n    canExport: false,\n  }\n}\n\nfunction statusName"
if old2 not in t:
    raise SystemExit('scheduled block missing: ' + repr(t[t.find('function statusName')-80:t.find('function statusName')+20]))
t = t.replace(old2, new2, 1)
p.write_text(t, encoding='utf-8')
print('Tasks.vue ok')
