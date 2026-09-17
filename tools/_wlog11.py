from pathlib import Path
t=Path('frontend/src/views/Tasks.vue').read_text(encoding='utf-8')
i=t.find('  detailDialog.value = {')
print('idx', i)
print(repr(t[i:i+180]))
print('logPageSize', 'logPageSize' in t)
