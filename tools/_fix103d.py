from pathlib import Path
p = Path('docs/developer/custom-change-log.md')
t = p.read_text(encoding='utf-8')
needle = '- 页面版本号改为 1.0.2。'
add = '''- 页面版本号改为 1.0.2。

## 1.0.3
- 素材图片发送时按所选手机型号、时间、倍率生成截屏样机；Android 可叠加 Telegram 通知标记。
- 活跃消息规则支持追加多段文字和多张图片/素材，执行时按顺序发出。
- 任务详情增加执行日志分页查看和 CSV 导出；批量建群过程写入 BatchTaskLogs。
- 群组资源列表隐藏用户名列。
- 页面版本号改为 1.0.3。
'''
if '## 1.0.3' in t:
    print('changelog exists')
else:
    if needle not in t: raise SystemExit('changelog needle missing')
    t = t.replace(needle, add, 1)
    p.write_text(t, encoding='utf-8')
    print('changelog ok')
p = Path('src/TelegramPanel.Web/TelegramPanel.Web.csproj')
t = p.read_text(encoding='utf-8')
if 'SixLabors.ImageSharp' not in t:
    t = t.replace('    <PackageReference Include="Serilog.AspNetCore" Version="8.*" />', '    <PackageReference Include="Serilog.AspNetCore" Version="8.*" />\n    <PackageReference Include="SixLabors.ImageSharp" Version="3.*" />', 1)
    p.write_text(t, encoding='utf-8')
    print('csproj ok')
else:
    print('csproj already')
