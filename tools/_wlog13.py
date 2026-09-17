from pathlib import Path
p=Path('frontend/src/components/TaskConfigForm.vue')
t=p.read_text(encoding='utf-8')
old='          <el-form-item label="图片字典">\n            <el-select v-model="rule.imageDictionaryName" class="full" placeholder="不发送图片">'
idx=t.find(old)
print('idx', idx)
print(t[t.find('素材底图')-20:t.find('素材底图')+80] if '素材底图' in t else 'no 底图')
