from pathlib import Path
p=Path('frontend/src/components/TaskConfigForm.vue')
t=p.read_text(encoding='utf-8')
t=t.replace('  insertDictionaryName?: string\n}', '  insertDictionaryName?: string\n  extraTexts?: Array<{ id: string; text: string }>\n  extraImages?: Array<{ id: string; imageDictionaryName: string; materialDictionaryName: string }>\n}', 1)
t=t.replace("    insertDictionaryName: '',\n  }", "    insertDictionaryName: '',\n    extraTexts: [],\n    extraImages: [],\n  }", 1)
fn='''function addExtraText(rule: UserChatActiveMessageRuleForm) {
  if (!rule.extraTexts) rule.extraTexts = []
  rule.extraTexts.push({ id: newScopeId(), text: '' })
}
function addExtraImage(rule: UserChatActiveMessageRuleForm) {
  if (!rule.extraImages) rule.extraImages = []
  rule.extraImages.push({ id: newScopeId(), imageDictionaryName: '', materialDictionaryName: '' })
}
'''
if 'function addExtraText' not in t:
    t=t.replace('function insertGroupEngagementDictionary', fn+'function insertGroupEngagementDictionary', 1)
old='material_notification: rule.materialNotification || \'none\' })), min_successful_invites'
if 'extra_texts' not in t:
    t=t.replace('material_notification: rule.materialNotification || \'none\' }', 'material_notification: rule.materialNotification || \'none\', extra_texts: (rule.extraTexts || []).map((x) => x.text).filter(Boolean), extra_images: (rule.extraImages || []).map((x) => ({ image_dictionary_token: x.imageDictionaryName ? dictionaryToken(x.imageDictionaryName) : null, material_dictionary_token: x.materialDictionaryName ? dictionaryToken(x.materialDictionaryName) : null, material_device: rule.materialDevice || \'iphone-16-pro-max\', material_time_mode: rule.materialTimeMode || \'now\', material_time: rule.materialTime || \'15:59\', material_scale: Number(rule.materialScale || 1), material_notification: rule.materialNotification || \'none\' })) }', 1)
p.write_text(t, encoding='utf-8')
print('extras script', 'addExtraText' in t, 'extra_texts' in t)
