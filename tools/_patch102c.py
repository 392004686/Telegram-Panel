from pathlib import Path
p = Path('src/TelegramPanel.Web/Services/DataDictionaryService.cs')
t = p.read_text(encoding='utf-8')
old = '''        return await SaveMediaDictionaryAsync(id, name, displayName, description, readMode, isEnabled, keepItemIds, newImages, DataDictionaryTypes.Image, "图片", cancellationToken);'''
new = '''        var type = string.Equals(dictionaryType, DataDictionaryTypes.Material, StringComparison.OrdinalIgnoreCase) ? DataDictionaryTypes.Material : DataDictionaryTypes.Image;
        var label = type == DataDictionaryTypes.Material ? "素材图片" : "图片";
        return await SaveMediaDictionaryAsync(id, name, displayName, description, readMode, isEnabled, keepItemIds, newImages, type, label, cancellationToken);'''
if old not in t: raise SystemExit('save call missing')
t = t.replace(old, new, 1)
old = '''        IReadOnlyList<DataDictionaryImageItemInput> newImages,
        CancellationToken cancellationToken = default)'''
new = '''        IReadOnlyList<DataDictionaryImageItemInput> newImages,
        CancellationToken cancellationToken = default,
        string? dictionaryType = null)'''
# only first occurrence is SaveImageDictionaryAsync
idx = t.find('public async Task<DataDictionary> SaveImageDictionaryAsync')
if idx < 0: raise SystemExit('method missing')
chunk = t[idx:idx+900]
if old not in chunk: raise SystemExit('sig missing '+repr(chunk[:400]))
t = t[:idx] + chunk.replace(old, new, 1) + t[idx+900:]
old = '''        if (!string.Equals(dictionary.Type, DataDictionaryTypes.Image, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"字典不是图片类型：{dictionary.Name}");'''
new = '''        if (!string.Equals(dictionary.Type, DataDictionaryTypes.Image, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(dictionary.Type, DataDictionaryTypes.Material, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"字典不是图片或素材类型：{dictionary.Name}");'''
if old not in t: raise SystemExit('resolve type missing')
t = t.replace(old, new, 1)
p.write_text(t, encoding='utf-8')
print('service ok')
