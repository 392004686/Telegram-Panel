from pathlib import Path
p = Path('frontend/src/config/appVersion.ts')
p.write_text("export const APP_RELEASE_VERSION = '1.0.2'\n", encoding='utf-8')
print(p.read_text(encoding='utf-8'))
