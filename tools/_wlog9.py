from pathlib import Path
p=Path('frontend/src/views/Tasks.vue')
t=p.read_text(encoding='utf-8')
old='''    <el-dialog v-model="detailDialog.visible" :title="detailDialog.title" width="min(720px, calc(100vw - 24px))">
      <pre class="detail-pre">{{ detailDialog.content }}</pre>
      <template #footer>
        <el-button type="primary" @click="detailDialog.visible = false">关闭</el-button>
      </template>
    </el-dialog>'''
new='''    <el-dialog v-model="detailDialog.visible" :title="detailDialog.title" width="min(920px, calc(100vw - 24px))">
      <pre class="detail-pre">{{ detailDialog.content }}</pre>
      <div class="log-toolbar"><strong>执行日志</strong>
        <el-button size="small" :disabled="!detailDialog.taskId || detailDialog.logTotal === 0" @click="exportTaskLogs">导出 CSV</el-button>
      </div>
      <el-table :data="detailDialog.logs" size="small" max-height="320" empty-text="暂无执行日志">
        <el-table-column label="时间" width="170"><template #default="{row}">{{ formatTime(row.createdAt) }}</template></el-table-column>
        <el-table-column prop="level" label="级别" width="80" />
        <el-table-column prop="message" label="内容" min-width="360" />
      </el-table>
      <div class="pager" v-if="detailDialog.logTotal > detailDialog.logPageSize">
        <el-pagination v-model:current-page="detailDialog.logPage" :page-size="detailDialog.logPageSize" layout="total, prev, pager, next" :total="detailDialog.logTotal" @current-change="loadTaskLogs" />
      </div>
      <template #footer>
        <el-button type="primary" @click="detailDialog.visible = false">关闭</el-button>
      </template>
    </el-dialog>'''
if old not in t: raise SystemExit('dialog missing')
t=t.replace(old,new,1)
p.write_text(t, encoding='utf-8')
print('dialog ok')
