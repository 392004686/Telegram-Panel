from pathlib import Path
p = Path('frontend/src/views/DataDictionaries.vue')
t = p.read_text(encoding='utf-8')

def sub(old, new):
    global t
    if old not in t:
        raise SystemExit('NO '+repr(old[:100]))
    t = t.replace(old, new, 1)

sub(":title=\"editor.form.type === 'image' ? '图片字典可一次或分批选择多张图片，供头像或消息任务按随机/队列模式取用。' : '视频字典可保存多个视频素材，后续支持视频的自动任务可按随机/队列模式取用。组合文字与媒体时应由任务的消息模板同时引用多个字典，不需要复制一份组合字典。\"", ":title=\"mediaEditorHint\"")
sub(":label=\"editor.form.type === 'image' ? '选择图片' : '选择视频'\"", ":label=\"isImageType(editor.form.type) ? '选择图片' : '选择视频'\"")
sub(":accept=\"editor.form.type === 'image' ? 'image/*' : 'video/mp4,video/quicktime,video/webm,.m4v,.mkv'\"", ":accept=\"isImageType(editor.form.type) ? 'image/*' : 'video/mp4,video/quicktime,video/webm,.m4v,.mkv'\"")
sub(":list-type=\"editor.form.type === 'image' ? 'picture-card' : 'text'\"", ":list-type=\"isImageType(editor.form.type) ? 'picture-card' : 'text'\"")
sub(":label=\"editor.form.type === 'image' ? '现有图片' : '现有视频'\"", ":label=\"isImageType(editor.form.type) ? '现有图片' : '现有视频'\"")
sub("<el-image v-if=\"editor.form.type === 'image'\"", "<el-image v-if=\"isImageType(editor.form.type)\"")
sub("type DictionaryType = 'text' | 'image' | 'video'", "type DictionaryType = 'text' | 'image' | 'material' | 'video'")
sub("  if (editor.form.type === 'image') return editor.form.id ? '编辑图片字典' : '新建图片字典'", "  if (editor.form.type === 'image') return editor.form.id ? '编辑图片字典' : '新建图片字典'\n  if (editor.form.type === 'material') return editor.form.id ? '编辑素材图片' : '新建素材图片'")
sub("  editor.form.type = row.type === 'image' ? 'image' : row.type === 'video' ? 'video' : 'text'", "  editor.form.type = row.type === 'image' ? 'image' : row.type === 'material' ? 'material' : row.type === 'video' ? 'video' : 'text'")
sub("`${editor.form.type === 'image' ? '图片' : '视频'}字典至少需要一个文件`", "`${isImageType(editor.form.type) ? (editor.form.type === 'material' ? '素材图片' : '图片') : '视频'}至少需要一个文件`")
sub("  files.forEach((file) => form.append(editor.form.type === 'image' ? 'images' : 'videos', file))", "  files.forEach((file) => form.append(isImageType(editor.form.type) ? 'images' : 'videos', file))\n  if (isImageType(editor.form.type)) form.append('dictionaryType', editor.form.type)")
sub("    if (editor.form.type === 'image') await panelApi.saveImageDictionary(form)\n    else await panelApi.saveVideoDictionary(form)\n    ElMessage.success('图片字典已保存')", "    if (isImageType(editor.form.type)) await panelApi.saveImageDictionary(form)\n    else await panelApi.saveVideoDictionary(form)\n    ElMessage.success(editor.form.type === 'material' ? '素材图片已保存' : '字典已保存')")
sub("function dictionaryTypeLabel(type: string) {\n  return type === 'image' ? '图片' : type === 'video' ? '视频' : '文本'\n}", "function isImageType(type: string) {\n  return type === 'image' || type === 'material'\n}\n\nconst mediaEditorHint = computed(() => {\n  if (editor.form.type === 'material') return '素材图片单独维护，上传后可改名、增删图片，并在建群活跃消息规则里直接选用，不必在创建任务时再传图。'\n  if (editor.form.type === 'image') return '图片字典可一次或分批选择多张图片，供头像或消息任务按随机/队列模式取用。'\n  return '视频字典可保存多个视频素材，后续支持视频的自动任务可按随机/队列模式取用。'\n})\n\nfunction dictionaryTypeLabel(type: string) {\n  return type === 'material' ? '素材' : type === 'image' ? '图片' : type === 'video' ? '视频' : '文本'\n}")
p.write_text(t, encoding='utf-8')
print('dict vue ok')
