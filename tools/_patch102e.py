from pathlib import Path
p = Path('frontend/src/views/DataDictionaries.vue')
t = p.read_text(encoding='utf-8')
t = t.replace(
    'title="填写方法：字典名称是任务中引用的变量名，例如 promo_text 对应 {promo_text}；显示名称仅用于页面识别。文本字典一行一条，图片/视频字典上传多个素材；随机模式每次随机取一项，队列模式按顺序轮换。"',
    'title="填写方法：字典名称是任务中引用的变量名，例如 promo_text 对应 {promo_text}。文本字典一行一条。图片字典给通用任务配图；素材图片单独维护建群/活跃消息用的固定图，可在线上传、改名、编辑、删除后在任务规则里直接选用。随机模式每次随机取一项，队列模式按顺序轮换。"'
)
t = t.replace(
    '        <el-button @click="openCreate(\'image\')">新建图片字典</el-button>\n        <el-button @click="openCreate(\'video\')">新建视频字典</el-button>',
    '        <el-button @click="openCreate(\'image\')">新建图片字典</el-button>\n        <el-button type="success" plain @click="openCreate(\'material\')">新建素材图片</el-button>\n        <el-button @click="openCreate(\'video\')">新建视频字典</el-button>'
)
t = t.replace(
    '<el-tag :type="row.type === \'image\' ? \'warning\' : row.type === \'video\' ? \'danger\' : \'primary\'" size="small">{{ dictionaryTypeLabel(row.type) }}</el-tag>',
    '<el-tag :type="row.type === \'material\' ? \'success\' : row.type === \'image\' ? \'warning\' : row.type === \'video\' ? \'danger\' : \'primary\'" size="small">{{ dictionaryTypeLabel(row.type) }}</el-tag>'
)
t = t.replace(
    '                <el-option label="图片字典" value="image" />\n                <el-option label="视频字典" value="video" />',
    '                <el-option label="图片字典" value="image" />\n                <el-option label="素材图片" value="material" />\n                <el-option label="视频字典" value="video" />'
)
print('partial', '新建素材图片' in t)
p.write_text(t, encoding='utf-8')
