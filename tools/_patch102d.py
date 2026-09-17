from pathlib import Path
p = Path('src/TelegramPanel.Web/Api/PanelAdminApiEndpoints.cs')
t = p.read_text(encoding='utf-8')
old = '        var keepItemIds = ParseIntList(form["keepItemIds"]);\n'
new = '        var keepItemIds = ParseIntList(form["keepItemIds"]);\n        var dictionaryType = form["dictionaryType"].ToString();\n'
# only in SaveImageDictionaryAsync
idx = t.find('private static async Task<IResult> SaveImageDictionaryAsync')
if idx < 0: raise SystemExit('api method missing')
end = t.find('private static async Task<IResult>', idx+10)
chunk = t[idx:end]
if old not in chunk: raise SystemExit('keepItemIds missing')
chunk2 = chunk.replace(old, new, 1)
old2 = '''                newImages,
                cancellationToken);'''
new2 = '''                newImages,
                cancellationToken,
                dictionaryType);'''
if old2 not in chunk2: raise SystemExit('call missing')
chunk2 = chunk2.replace(old2, new2, 1)
t = t[:idx] + chunk2 + t[end:]
p.write_text(t, encoding='utf-8')
print('api ok')

p = Path('src/TelegramPanel.Web/Services/TemplateRenderingService.cs')
t = p.read_text(encoding='utf-8')
old = '''        if (!string.Equals(dictionary.Type, expectedType, StringComparison.OrdinalIgnoreCase))
        {
            var expectedName = string.Equals(expectedType, DataDictionaryTypes.Image, StringComparison.OrdinalIgnoreCase)
                ? "图片字典"
                : "文本字典";
            throw new InvalidOperationException($"变量类型不匹配：{{{tokenName}}} 需要使用{expectedName}");
        }'''
new = '''        var typeMatches = string.Equals(dictionary.Type, expectedType, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(expectedType, DataDictionaryTypes.Image, StringComparison.OrdinalIgnoreCase)
                && string.Equals(dictionary.Type, DataDictionaryTypes.Material, StringComparison.OrdinalIgnoreCase));
        if (!typeMatches)
        {
            var expectedName = string.Equals(expectedType, DataDictionaryTypes.Image, StringComparison.OrdinalIgnoreCase)
                ? "图片/素材字典"
                : "文本字典";
            throw new InvalidOperationException($"变量类型不匹配：{{{tokenName}}} 需要使用{expectedName}");
        }'''
if old not in t: raise SystemExit('template missing')
p.write_text(t.replace(old, new, 1), encoding='utf-8')
print('template ok')
