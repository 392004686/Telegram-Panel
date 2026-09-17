from pathlib import Path

def sub(path, old, new):
    t = Path(path).read_text(encoding='utf-8')
    if old not in t: raise SystemExit('MISS '+path+' '+old[:70])
    Path(path).write_text(t.replace(old, new, 1), encoding='utf-8')
    print('ok', path)

sub('src/TelegramPanel.Web/Services/CustomerGroupEngagementTaskHandler.cs',
    '        [JsonPropertyName("material_notification")]\n        public string? MaterialNotification { get; set; }\n    }',
    '        [JsonPropertyName("material_notification")]\n        public string? MaterialNotification { get; set; }\n        [JsonPropertyName("extra_texts")]\n        public List<string> ExtraTexts { get; set; } = [];\n        [JsonPropertyName("extra_images")]\n        public List<MessageRule> ExtraImages { get; set; } = [];\n    }')

sub('frontend/src/api/panel.ts',
    '  deleteTask: (id: number) => api.delete<OperationResult>(`/tasks/${id}`).then((r) => r.data),',
    '  deleteTask: (id: number) => api.delete<OperationResult>(`/tasks/${id}`).then((r) => r.data),\n  taskLogs: (id: number, params: { page: number; pageSize: number }) => api.get<{ items: Array<{ id: number; level: string; message: string; createdAt: string }>; total: number; page: number; pageSize: number }>(`/tasks/${id}/logs`, { params }).then((r) => r.data),')

sub('frontend/src/views/ChatResources.vue',
    "  { key: 'username', label: '用户名' },",
    "  ...(kind === 'group' ? [] : [{ key: 'username', label: '用户名' }]),")
