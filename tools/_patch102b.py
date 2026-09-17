from pathlib import Path

def sub(path, old, new):
    p = Path(path)
    t = p.read_text(encoding='utf-8')
    if old not in t:
        raise SystemExit('MISSING ' + path + ' :: ' + repr(old[:120]))
    p.write_text(t.replace(old, new, 1), encoding='utf-8')
    print('ok', path)

sub(
    'src/TelegramPanel.Data/Entities/DataDictionary.cs',
    '    public const string Video = "video";\n}',
    '    public const string Video = "video";\n    public const string Material = "material";\n}',
)
