from pathlib import Path
p = Path('frontend/src/api/types.ts')
t = p.read_text(encoding='utf-8')
old = '  autoPauseBeforeEdit: boolean\n}'
new = '  autoPauseBeforeEdit: boolean\n  order?: number\n}'
if old not in t: raise SystemExit('typedef missing')
t = t.replace(old, new, 1)
p.write_text(t, encoding='utf-8')
p = Path('frontend/src/views/CustomerLookup.vue')
t = p.read_text(encoding='utf-8')
old = ':selectable="row => !active(row)"'
new = ':selectable="(row: CustomerLookupBatch) => !active(row)"'
if old not in t: raise SystemExit('selectable missing')
t = t.replace(old, new, 1)
p.write_text(t, encoding='utf-8')
print('fixed')
