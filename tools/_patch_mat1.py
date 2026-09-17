from pathlib import Path

def sub(path, old, new):
    p = Path(path)
    t = p.read_text(encoding='utf-8')
    if old not in t:
        raise SystemExit('missing '+path+' '+repr(old[:80]))
    p.write_text(t.replace(old, new, 1), encoding='utf-8')
    print('ok', path)

sub('src/TelegramPanel.Web/Program.cs',
    'builder.Services.AddSingleton<ImageAssetStorageService>();',
    'builder.Services.AddSingleton<ImageAssetStorageService>();\nbuilder.Services.AddSingleton<MaterialMockupService>();')

sub('src/TelegramPanel.Web/Services/CustomerGroupEngagementTaskHandler.cs',
    '        [JsonPropertyName("image_dictionary_token")]\n        public string? ImageDictionaryToken { get; set; }',
    '        [JsonPropertyName("image_dictionary_token")]\n        public string? ImageDictionaryToken { get; set; }\n        [JsonPropertyName("material_dictionary_token")]\n        public string? MaterialDictionaryToken { get; set; }\n        [JsonPropertyName("material_device")]\n        public string? MaterialDevice { get; set; }\n        [JsonPropertyName("material_time_mode")]\n        public string? MaterialTimeMode { get; set; }\n        [JsonPropertyName("material_time")]\n        public string? MaterialTime { get; set; }\n        [JsonPropertyName("material_scale")]\n        public double MaterialScale { get; set; } = 1;\n        [JsonPropertyName("material_notification")]\n        public string? MaterialNotification { get; set; }')
