from pathlib import Path
p = Path('frontend/src/components/TaskConfigForm.vue')
t = p.read_text(encoding='utf-8')

old = '''          <el-form-item label="插入文字字典">
            <div class="dict-insert">
              <el-select v-model="rule.insertDictionaryName" class="full" clearable placeholder="选择要插入的文字字典">
                <el-option v-for="name in textDictionaryNames" :key="name" :label="name" :value="name" />
              </el-select>
              <el-button @click="insertGroupEngagementDictionary(rule)">插入</el-button>
            </div>
          </el-form-item>
          <el-form-item label="图片字典">
            <el-select v-model="rule.imageDictionaryName" class="full" placeholder="不发送图片">
              <el-option label="不发送图片" value="" />
              <el-option v-for="name in imageDictionaryNames" :key="name" :label="name" :value="name" />
            </el-select>
          </el-form-item>'''
new = '''          <el-form-item label="插入文字字典">
            <el-select v-model="rule.insertDictionaryName" class="full" clearable placeholder="选择文字字典后点插入">
              <el-option v-for="name in textDictionaryNames" :key="name" :label="name" :value="name" />
            </el-select>
            <div class="form-hint no-offset compact dict-insert-actions">
              <el-button @click="insertGroupEngagementDictionary(rule)">插入到消息内容</el-button>
              下拉宽度与图片字典一致，插入后仍可继续改文字。
            </div>
          </el-form-item>
          <el-form-item label="素材图片">
            <el-select v-model="rule.materialDictionaryName" class="full" placeholder="不使用素材图片">
              <el-option label="不使用素材图片" value="" />
              <el-option v-for="name in materialDictionaryNames" :key="name" :label="name" :value="name" />
            </el-select>
            <div class="form-hint no-offset compact">在「数据字典」新建/编辑/删除素材图片后，这里直接选用，创建任务时不必再上传。</div>
          </el-form-item>
          <el-form-item label="图片字典">
            <el-select v-model="rule.imageDictionaryName" class="full" placeholder="不发送图片">
              <el-option label="不发送图片" value="" />
              <el-option v-for="name in imageDictionaryNames" :key="name" :label="name" :value="name" />
            </el-select>
          </el-form-item>'''
if old not in t: raise SystemExit('form block missing')
t = t.replace(old, new, 1)

t = t.replace("interface UserChatActiveMessageRuleForm {\n  id: string\n  text: string\n  imageDictionaryName: string\n  insertDictionaryName?: string\n}",
              "interface UserChatActiveMessageRuleForm {\n  id: string\n  text: string\n  imageDictionaryName: string\n  materialDictionaryName?: string\n  insertDictionaryName?: string\n}")

old = '''const imageDictionaryNames = computed(() =>
  dictionaries.value
    .filter((x) => x.isEnabled && x.type === 'image' && x.enabledItemCount > 0)
    .map((x) => x.name)
    .sort((a, b) => a.localeCompare(b, 'zh-Hans-CN')),
)'''
new = '''const imageDictionaryNames = computed(() =>
  dictionaries.value
    .filter((x) => x.isEnabled && x.type === 'image' && x.enabledItemCount > 0)
    .map((x) => x.name)
    .sort((a, b) => a.localeCompare(b, 'zh-Hans-CN')),
)
const materialDictionaryNames = computed(() =>
  dictionaries.value
    .filter((x) => x.isEnabled && x.type === 'material' && x.enabledItemCount > 0)
    .map((x) => x.name)
    .sort((a, b) => a.localeCompare(b, 'zh-Hans-CN')),
)'''
if old not in t: raise SystemExit('image names missing')
t = t.replace(old, new, 1)

t = t.replace("      ? rules.map((rule: any) => defaultUserChatActiveMessageRule(readString(rule?.text), extractDictionaryName(readString(rule?.image_dictionary_token))))",
              "      ? rules.map((rule: any) => { const token = extractDictionaryName(readString(rule?.image_dictionary_token)); const r = defaultUserChatActiveMessageRule(readString(rule?.text), token); if (token && materialDictionaryNames.value.includes(token)) { r.materialDictionaryName = token; r.imageDictionaryName = '' } return r })")

old = '''function normalizeUserChatActiveMessageRules(rules: UserChatActiveMessageRuleForm[]) {
  return rules
    .map((rule) => ({
      text: normalizeMultilineText(rule.text),
      imageDictionaryName: rule.imageDictionaryName.trim(),
    }))
    .filter((rule) => rule.text || rule.imageDictionaryName)
}'''
new = '''function normalizeUserChatActiveMessageRules(rules: UserChatActiveMessageRuleForm[]) {
  return rules
    .map((rule) => ({
      text: normalizeMultilineText(rule.text),
      imageDictionaryName: (rule.materialDictionaryName || rule.imageDictionaryName || '').trim(),
      materialDictionaryName: (rule.materialDictionaryName || '').trim(),
    }))
    .filter((rule) => rule.text || rule.imageDictionaryName)
}'''
if old not in t: raise SystemExit('normalize missing')
t = t.replace(old, new, 1)

t = t.replace("    imageDictionaryName,\n    insertDictionaryName: '',\n  }", "    imageDictionaryName,\n    materialDictionaryName: '',\n    insertDictionaryName: '',\n  }")

t = t.replace(".dict-insert{display:flex;gap:8px;align-items:center}\n.dict-insert .full{flex:1}",
              ".dict-insert-actions{display:flex;align-items:center;gap:10px;margin-top:8px;flex-wrap:wrap}")

t = t.replace("  if (messageRules.length === 0) throw new Error(\"请至少添加一条活跃消息规则\")\n  for (const rule of messageRules) {\n    if (rule.imageDictionaryName && !imageDictionaryNames.value.includes(rule.imageDictionaryName)) throw new Error(\"请选择有效的图片字典\")\n  }",
              "  if (messageRules.length === 0) throw new Error(\"请至少添加一条活跃消息规则\")\n  for (const rule of messageRules) {\n    const pic = rule.materialDictionaryName || rule.imageDictionaryName\n    if (pic && !imageDictionaryNames.value.includes(pic) && !materialDictionaryNames.value.includes(pic)) throw new Error(\"请选择有效的素材图片或图片字典\")\n  }")

p.write_text(t, encoding='utf-8')
print('form ok')
