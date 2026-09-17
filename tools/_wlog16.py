from pathlib import Path
p=Path('src/TelegramPanel.Web/Services/CustomerGroupEngagementTaskHandler.cs')
t=p.read_text(encoding='utf-8')
old='                        await Delay(config, ct);\n                    }'
# this might match multiple. count
print('delay count', t.count(old) if False else t.count('                        await Delay(config, ct);'))
