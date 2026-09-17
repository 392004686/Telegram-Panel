from pathlib import Path
p = Path('frontend/src/views/DataDictionaries.vue')
t = p.read_text(encoding='utf-8')
start = t.find('            :title="mediaEditorHint" ignore=')
if start < 0: raise SystemExit('broken title not found')
end = t.find('\n', start)
t = t[:start] + '            :title="mediaEditorHint"' + t[end:]
p.write_text(t, encoding='utf-8')
print('fixed title')
