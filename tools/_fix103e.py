from pathlib import Path
p = Path('frontend/src/components/TaskConfigForm.vue')
t = p.read_text(encoding='utf-8')
t = t.replace('@click="rule.extraTexts.splice(pIndex, 1)"', '@click="removeExtraText(rule, pIndex)"')
t = t.replace('@click="rule.extraImages.splice(pIndex, 1)"', '@click="removeExtraImage(rule, pIndex)"')
old = '''function addExtraImage(rule: UserChatActiveMessageRuleForm) {
  if (!rule.extraImages) rule.extraImages = []
  rule.extraImages.push({ id: newScopeId(), imageDictionaryName: '', materialDictionaryName: '' })
}'''
new = '''function addExtraImage(rule: UserChatActiveMessageRuleForm) {
  if (!rule.extraImages) rule.extraImages = []
  rule.extraImages.push({ id: newScopeId(), imageDictionaryName: '', materialDictionaryName: '' })
}
function removeExtraText(rule: UserChatActiveMessageRuleForm, index: number) {
  rule.extraTexts?.splice(index, 1)
}
function removeExtraImage(rule: UserChatActiveMessageRuleForm, index: number) {
  rule.extraImages?.splice(index, 1)
}'''
if old not in t: raise SystemExit('addExtraImage missing')
t = t.replace(old, new, 1)
p.write_text(t, encoding='utf-8')
p = Path('frontend/src/views/Tasks.vue')
t = p.read_text(encoding='utf-8')
t = t.replace(".replaceAll('\"','\"\"')", ".split('\"').join('\"\"')")
if ".replaceAll('""" in t: raise SystemExit('replaceAll still there')
p.write_text(t, encoding='utf-8')
print('ts fixes ok')
