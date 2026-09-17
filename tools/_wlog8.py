from pathlib import Path
p=Path('frontend/src/views/ChatResources.vue')
t=p.read_text(encoding='utf-8')
t=t.replace("...(kind === 'group' ? [] : [{ key: 'username', label: '用户名' }]),", "...(props.kind === 'group' ? [] : [{ key: 'username', label: '用户名' }]),", 1)
p.write_text(t, encoding='utf-8')
print('kind fix', "props.kind === 'group'" in t)
