from pathlib import Path
root = Path(r'E:\Desktop\chongzhi\telegram_tool\work\Telegram-Panel')
p = root / 'frontend/src/components/TaskConfigForm.vue'
t = p.read_text(encoding='utf-8')
old = '''          <el-form-item label="图片字典">
            <el-select v-model="rule.imageDictionaryName" class="full" placeholder="不发送图片">
              <el-option label="不发送图片" value="" />
              <el-option v-for="name in imageDictionaryNames" :key="name" :label="name" :value="name" />
            </el-select>
          </el-form-item>
        </div>
          <div class="extra-parts">
'''
new = '''          <el-form-item label="图片字典">
            <el-select v-model="rule.imageDictionaryName" class="full" placeholder="不发送图片">
              <el-option label="不发送图片" value="" />
              <el-option v-for="name in imageDictionaryNames" :key="name" :label="name" :value="name" />
            </el-select>
          </el-form-item>
          <div class="extra-parts">
'''
if old not in t:
    raise SystemExit('form template mark missing')
t = t.replace(old, new, 1)
old2 = '''            <div class="form-hint no-offset compact">
              <el-button size="small" @click="addExtraText(rule)">添加文字</el-button>
              <el-button size="small" @click="addExtraImage(rule)">添加图片</el-button>
              每条规则可追加多段文字和多张图片，发送时按顺序发出。
            </div>
          </div>
        <el-form-item label="批量追加">'''
new2 = '''            <div class="form-hint no-offset compact">
              <el-button size="small" @click="addExtraText(rule)">添加文字</el-button>
              <el-button size="small" @click="addExtraImage(rule)">添加图片</el-button>
              每条规则可追加多段文字和多张图片，发送时按顺序发出。
            </div>
          </div>
        </div>
        <el-form-item label="批量追加">'''
if old2 not in t:
    raise SystemExit('form close mark missing')
t = t.replace(old2, new2, 1)
needle = "r.materialNotification = readString(rule?.material_notification) || 'none'; return r })"
repl = "r.materialNotification = readString(rule?.material_notification) || 'none'; r.extraTexts = Array.isArray(rule?.extra_texts) ? rule.extra_texts.filter((x: unknown) => String(x || '').trim()).map((text: string) => ({ id: newScopeId(), text: String(text) })) : []; r.extraImages = Array.isArray(rule?.extra_images) ? rule.extra_images.map((x: any) => ({ id: newScopeId(), imageDictionaryName: extractDictionaryName(readString(x?.image_dictionary_token)), materialDictionaryName: extractDictionaryName(readString(x?.material_dictionary_token)) })) : []; return r })"
if needle not in t:
    raise SystemExit('applyInitialConfig mark missing')
t = t.replace(needle, repl, 1)
if '.extra-parts{' not in t and '.extra-parts {' not in t:
    t = t.replace('.message-rule-card {', '.extra-parts{margin-top:8px}.extra-part{margin-bottom:8px}\n.message-rule-card {', 1)
p.write_text(t, encoding='utf-8')
print('TaskConfigForm ok')
