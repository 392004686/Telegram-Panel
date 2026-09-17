from pathlib import Path
p = Path('docs/developer/custom-change-log.md')
t = p.read_text(encoding='utf-8')
note = '\n\n## 1.0.2\n- 数据字典新增「素材图片」类型：可在线新建、上传、改名、编辑、删除，供建群活跃消息规则直接选用。\n- 批量建群活跃规则增加素材图片选择；插入文字字典下拉改为与图片字典同宽。\n- 页面版本号改为 1.0.2。\n'
if '## 1.0.2' not in t: p.write_text(t.rstrip()+note, encoding='utf-8')
print('log ok')
p = Path('frontend/src/views/DataDictionaries.vue')
t = p.read_text(encoding='utf-8')
print('computed import', 'computed' in t[:2500])
print('alert', [ln for ln in t.splitlines() if 'mediaEditorHint' in ln][:3])
