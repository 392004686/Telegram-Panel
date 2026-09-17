from pathlib import Path
p = Path('frontend/src/components/TaskConfigForm.vue')
t = p.read_text(encoding='utf-8')
old = '''          <el-form-item label="素材图片">
            <el-select v-model="rule.materialDictionaryName" class="full" placeholder="不使用素材图片">
              <el-option label="不使用素材图片" value="" />
              <el-option v-for="name in materialDictionaryNames" :key="name" :label="name" :value="name" />
            </el-select>
            <div class="form-hint no-offset compact">在「数据字典」新建/编辑/删除素材图片后，这里直接选用，创建任务时不必再上传。</div>
          </el-form-item>'''
new = '''          <el-form-item label="素材底图">
            <el-select v-model="rule.materialDictionaryName" class="full" placeholder="不使用素材底图">
              <el-option label="不使用素材底图" value="" />
              <el-option v-for="name in materialDictionaryNames" :key="name" :label="name" :value="name" />
            </el-select>
            <div class="form-hint no-offset compact">在「数据字典」上传底图（商品页截图）。发送时按下面参数生成手机截屏图，不必在创建任务时再传图。</div>
          </el-form-item>
          <template v-if="rule.materialDictionaryName">
            <el-row :gutter="12">
              <el-col :span="12">
                <el-form-item label="手机型号">
                  <el-select v-model="rule.materialDevice" class="full">
                    <el-option v-for="item in materialDevices" :key="item.id" :label="item.label" :value="item.id" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="生成时间">
                  <el-radio-group v-model="rule.materialTimeMode">
                    <el-radio-button value="now">发送时当前时间</el-radio-button>
                    <el-radio-button value="fixed">固定时间</el-radio-button>
                  </el-radio-group>
                </el-form-item>
              </el-col>
            </el-row>
            <el-row :gutter="12">
              <el-col :span="8">
                <el-form-item v-if="rule.materialTimeMode === 'fixed'" label="固定时刻">
                  <el-input v-model="rule.materialTime" placeholder="15:59" class="full" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="倍率">
                  <el-select v-model="rule.materialScale" class="full">
                    <el-option :value="0.5" label="0.5x" />
                    <el-option :value="1" label="1x" />
                    <el-option :value="1.5" label="1.5x" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="Android 通知">
                  <el-select v-model="rule.materialNotification" class="full" :disabled="!isAndroidMaterialDevice(rule.materialDevice)">
                    <el-option value="none" label="无" />
                    <el-option value="telegram" label="Telegram 小飞机" />
                  </el-select>
                </el-form-item>
              </el-col>
            </el-row>
          </template>'''
if old not in t: raise SystemExit('material form missing')
t = t.replace(old, new, 1)

t = t.replace("  materialDictionaryName?: string\n  insertDictionaryName?: string",
              "  materialDictionaryName?: string\n  materialDevice?: string\n  materialTimeMode?: string\n  materialTime?: string\n  materialScale?: number\n  materialNotification?: string\n  insertDictionaryName?: string")

old = '''const materialDictionaryNames = computed(() =>
  dictionaries.value
    .filter((x) => x.isEnabled && x.type === 'material' && x.enabledItemCount > 0)
    .map((x) => x.name)
    .sort((a, b) => a.localeCompare(b, 'zh-Hans-CN')),
)'''
new = '''const materialDictionaryNames = computed(() =>
  dictionaries.value
    .filter((x) => x.isEnabled && x.type === 'material' && x.enabledItemCount > 0)
    .map((x) => x.name)
    .sort((a, b) => a.localeCompare(b, 'zh-Hans-CN')),
)
const materialDevices = [
  { id: 'iphone-16-pro-max', label: 'iPhone 16 Pro Max' },
  { id: 'iphone-16-pro', label: 'iPhone 16 Pro' },
  { id: 'iphone-16-plus', label: 'iPhone 16 Plus' },
  { id: 'iphone-16', label: 'iPhone 16' },
  { id: 'iphone-15-pro-max', label: 'iPhone 15 Pro Max' },
  { id: 'iphone-14-pro-max', label: 'iPhone 14 Pro Max' },
  { id: 'pixel-7-pro', label: 'Pixel 7 Pro' },
  { id: 'pixel-8-pro', label: 'Pixel 8 Pro' },
  { id: 'galaxy-s24', label: 'Galaxy S24' },
  { id: 'galaxy-a55', label: 'Galaxy A55' },
]
function isAndroidMaterialDevice(id?: string) {
  return (id || '').startsWith('pixel-') || (id || '').startsWith('galaxy-')
}'''
if old not in t: raise SystemExit('material names missing')
t = t.replace(old, new, 1)

t = t.replace("      ? rules.map((rule: any) => { const token = extractDictionaryName(readString(rule?.image_dictionary_token)); const r = defaultUserChatActiveMessageRule(readString(rule?.text), token); if (token && materialDictionaryNames.value.includes(token)) { r.materialDictionaryName = token; r.imageDictionaryName = '' } return r })",
              "      ? rules.map((rule: any) => { const materialToken = extractDictionaryName(readString(rule?.material_dictionary_token)); const imageToken = extractDictionaryName(readString(rule?.image_dictionary_token)); const r = defaultUserChatActiveMessageRule(readString(rule?.text), imageToken); if (materialToken) { r.materialDictionaryName = materialToken; r.imageDictionaryName = '' } r.materialDevice = readString(rule?.material_device) || 'iphone-16-pro-max'; r.materialTimeMode = readString(rule?.material_time_mode) || 'now'; r.materialTime = readString(rule?.material_time) || '15:59'; r.materialScale = Number(rule?.material_scale || 1); r.materialNotification = readString(rule?.material_notification) || 'none'; return r })")

t = t.replace("    materialDictionaryName: '',\n    insertDictionaryName: '',",
              "    materialDictionaryName: '',\n    materialDevice: 'iphone-16-pro-max',\n    materialTimeMode: 'now',\n    materialTime: '15:59',\n    materialScale: 1,\n    materialNotification: 'none',\n    insertDictionaryName: '',")

t = t.replace("  const config = { account_ids: ids, account_category_id: f.accountCategoryId || null, account_category_name: accountCategoryName, customer_group_ids: f.customerGroupIds, customer_group_names: customerGroupNames, customers_per_group: f.customersPerGroup, assignment_mode: f.assignmentMode, worker_count: f.workerCount, group_title_template: f.groupTitleTemplate, group_about_template: f.groupAboutTemplate, activity_messages: messageRules.map((x) => x.text).filter(Boolean), message_rules: messageRules.map((rule) => ({ text: rule.text, image_dictionary_token: rule.imageDictionaryName ? dictionaryToken(rule.imageDictionaryName) : null })), min_successful_invites: f.minSuccessfulInvites, min_delay_seconds: f.minDelaySeconds, max_delay_seconds: f.maxDelaySeconds }",
              "  const config = { account_ids: ids, account_category_id: f.accountCategoryId || null, account_category_name: accountCategoryName, customer_group_ids: f.customerGroupIds, customer_group_names: customerGroupNames, customers_per_group: f.customersPerGroup, assignment_mode: f.assignmentMode, worker_count: f.workerCount, group_title_template: f.groupTitleTemplate, group_about_template: f.groupAboutTemplate, activity_messages: messageRules.map((x) => x.text).filter(Boolean), message_rules: f.messageRules.map((rule) => ({ text: rule.text, image_dictionary_token: rule.imageDictionaryName ? dictionaryToken(rule.imageDictionaryName) : null, material_dictionary_token: rule.materialDictionaryName ? dictionaryToken(rule.materialDictionaryName) : null, material_device: rule.materialDevice || 'iphone-16-pro-max', material_time_mode: rule.materialTimeMode || 'now', material_time: rule.materialTime || '15:59', material_scale: Number(rule.materialScale || 1), material_notification: rule.materialNotification || 'none' })), min_successful_invites: f.minSuccessfulInvites, min_delay_seconds: f.minDelaySeconds, max_delay_seconds: f.maxDelaySeconds }")

p.write_text(t, encoding='utf-8')
print('form ok')
