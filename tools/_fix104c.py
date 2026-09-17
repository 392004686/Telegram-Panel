from pathlib import Path
Path('frontend/src/config/appVersion.ts').write_text("export const APP_RELEASE_VERSION = '1.0.4'\n", encoding='utf-8')
p = Path('docs/developer/custom-change-log.md')
t = p.read_text(encoding='utf-8')
needle = '- 页面版本号改为 1.0.3。'
add = '''- 页面版本号改为 1.0.3。

## 1.0.4
- 批量建群在所选分类没有未沟通客户时改为失败并写执行日志，不再空跑后标成已完成 0/N。
- 创建任务时按未沟通人数计算任务总数；客户分类下拉显示未沟通/合计，全部已沟通时不允许提交。
- 页面版本号改为 1.0.4。
'''
if '## 1.0.4' not in t:
    if needle not in t: raise SystemExit('changelog needle missing')
    t = t.replace(needle, add, 1)
    p.write_text(t, encoding='utf-8')
print('version ok')
