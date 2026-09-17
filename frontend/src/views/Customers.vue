<template>
  <div class="customers-page">
    <el-alert v-if="loadError" :title="loadError" type="error" show-icon :closable="false" class="mb">
      <el-button size="small" @click="loadAll">重新加载</el-button>
    </el-alert>
    <el-card shadow="never" class="page-card">
      <div class="filter-row">
        <el-select v-model="filters.groupId" clearable placeholder="全部分类" class="filter">
          <el-option v-for="g in groups" :key="g.id" :label="g.name" :value="g.id"/>
        </el-select>
        <el-select v-model="filters.status" clearable placeholder="查询状态" class="filter">
          <el-option label="待查询" value="pending"/>
          <el-option label="已确认" value="found"/>
          <el-option label="未确认" value="not_found"/>
          <el-option label="查询异常" value="error"/>
        </el-select>
        <el-select v-model="filters.interaction" clearable placeholder="执行状态" class="filter">
          <el-option label="未执行" value="uncontacted"/>
          <el-option label="已沟通" value="contacted"/>
        </el-select>
        <el-select v-model="filters.batchId" clearable placeholder="导入批次" class="filter">
          <el-option v-for="b in batches" :key="b.id" :label="b.name" :value="b.id"/>
        </el-select>
        <el-input v-model="filters.search" clearable placeholder="搜索手机号、用户名、姓名、用户 ID" class="search" @keyup.enter="search"/>
        <el-button type="primary" @click="search">查询</el-button>
        <el-button @click="reset">重置</el-button>
        <el-button @click="clearSelection">取消勾选</el-button>
      </div>
    </el-card>
    <el-card shadow="never" class="page-card action-card">
      <div class="action-bar">
        <el-button type="primary" @click="importDialog.visible=true">批量导入</el-button>
        <el-button @click="selectCurrentPage">全选本页</el-button>
        <el-button @click="clearSelection">取消全选</el-button>
        <el-button :disabled="!selectedIds.length" @click="openBatchGroup">批量修改分类（已选）</el-button>
        <el-button type="danger" plain :disabled="!selectedIds.length" @click="batchDelete">批量删除（已选）</el-button>
        <el-dropdown trigger="click" @command="handleBatchCommand">
          <el-button>
            批量操作<el-icon class="el-icon--right"><ArrowDown /></el-icon>
          </el-button>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="select-unexecuted-page">勾选本页未执行数据</el-dropdown-item>
              <el-dropdown-item command="select-group-contacted">勾选当前组已沟通</el-dropdown-item>
              <el-dropdown-item command="select-group-unexecuted">勾选当前组未执行</el-dropdown-item>
              <el-dropdown-item command="mark-contacted" :disabled="!selectedIds.length">标记已选为已沟通</el-dropdown-item>
              <el-dropdown-item command="mark-unexecuted" :disabled="!selectedIds.length">标记已选为未执行</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
        <span class="muted">已选 {{ selectedIds.length }} 项，共 {{ total }} 个客户</span>
      </div>
    </el-card>
    <el-card shadow="never" class="page-card">
      <el-table ref="customerTable" v-loading="loading" :data="rows" row-key="id" stripe class="customers-table" @selection-change="onSelection">
        <el-table-column type="selection" width="48" reserve-selection/>
        <el-table-column prop="id" label="编号" width="75"/>
        <el-table-column prop="telegramUserId" label="用户ID" min-width="125"><template #default="{row}">{{row.telegramUserId||'-'}}</template></el-table-column>
        <el-table-column prop="displayName" label="姓名" min-width="130"><template #default="{row}">{{row.displayName||'-'}}</template></el-table-column>
        <el-table-column label="用户名" min-width="135"><template #default="{row}">{{row.username?'@'+row.username:'-'}}</template></el-table-column>
        <el-table-column prop="phone" label="手机号" min-width="145"><template #default="{row}">{{row.phone||'-'}}</template></el-table-column>
        <el-table-column label="头像" width="75"><template #default="{row}">{{yesNo(row.hasPhoto)}}</template></el-table-column>
        <el-table-column label="活跃状态" min-width="210"><template #default="{row}">{{activity(row)}}</template></el-table-column>
        <el-table-column label="Premium" width="92"><template #default="{row}">{{yesNo(row.isPremium)}}</template></el-table-column>
        <el-table-column label="查询状态" width="100"><template #default="{row}"><el-tag :type="statusType(row.lookupStatus)">{{statusLabel(row.lookupStatus)}}</el-tag></template></el-table-column>
        <el-table-column label="执行状态" width="100"><template #default="{row}"><el-tag :type="row.interactionStatus==='contacted'?'success':'info'">{{interactionLabel(row.interactionStatus)}}</el-tag></template></el-table-column>
        <el-table-column label="分类" min-width="130"><template #default="{row}"><el-tag v-for="g in row.groups" :key="g.id" class="tag" effect="plain">{{g.name}}</el-tag><span v-if="!row.groups.length">未分类</span></template></el-table-column>
        <el-table-column label="操作" width="160" fixed="right"><template #default="{row}"><div class="row-actions"><el-button link type="primary" @click="showDetail(row.id)">查看详情</el-button><el-button link type="danger" @click="remove(row)">删除</el-button></div></template></el-table-column>
      </el-table>
      <div class="pager"><span>共 {{total}} 条</span><el-pagination v-model:current-page="filters.page" v-model:page-size="filters.pageSize" layout="sizes, prev, pager, next" :total="total" :page-sizes="[20,50,100]" @change="loadCustomers"/></div>
    </el-card>
    <el-dialog v-model="importDialog.visible" title="批量导入客户" width="min(680px, calc(100vw - 24px))">
      <el-alert title="每行一个手机号或 @用户名；手机号允许空格和可选开头 +。" type="info" :closable="false"/>
      <el-form label-position="top" class="dialog-form">
        <el-form-item label="批次名称"><el-input v-model="importDialog.batchName" placeholder="留空自动生成"/></el-form-item>
        <el-form-item label="客户分类"><el-select v-model="importDialog.groupId" clearable class="full"><el-option v-for="g in groups" :key="g.id" :label="g.name" :value="g.id"/></el-select></el-form-item>
        <el-form-item label="手机号 / @用户名"><el-input v-model="importDialog.values" type="textarea" :rows="12" placeholder="+1 212 555 0123&#10;@username"/></el-form-item>
      </el-form>
      <template #footer><el-button @click="importDialog.visible=false">取消</el-button><el-button type="primary" :loading="importDialog.saving" @click="submitImport">导入</el-button></template>
    </el-dialog>
    <el-dialog v-model="detailDialog.visible" title="客户详情" width="min(760px, calc(100vw - 24px))">
      <el-skeleton v-if="detailDialog.loading" :rows="6" animated/>
      <el-descriptions v-else-if="detailDialog.data" :column="2" border>
        <el-descriptions-item label="用户 ID">{{detailDialog.data.telegramUserId||'-'}}</el-descriptions-item>
        <el-descriptions-item label="姓名">{{detailDialog.data.displayName||'-'}}</el-descriptions-item>
        <el-descriptions-item label="查询状态">{{statusLabel(detailDialog.data.lookupStatus)}}</el-descriptions-item>
        <el-descriptions-item label="执行状态">{{interactionLabel(detailDialog.data.interactionStatus)}}</el-descriptions-item>
        <el-descriptions-item label="用户名">{{detailDialog.data.username?'@'+detailDialog.data.username:'-'}}</el-descriptions-item>
        <el-descriptions-item label="手机号">{{detailDialog.data.phone||'-'}}</el-descriptions-item>
        <el-descriptions-item label="活跃状态">{{activity(detailDialog.data)}}</el-descriptions-item>
        <el-descriptions-item label="生日">{{detailDialog.data.birthday||'-'}}</el-descriptions-item>
      </el-descriptions>
    </el-dialog>
    <el-dialog v-model="batchGroup.visible" title="批量修改客户分类" width="440px">
      <el-select v-model="batchGroup.groupId" clearable class="full" placeholder="清空选择表示未分类"><el-option v-for="g in groups" :key="g.id" :label="g.name" :value="g.id"/></el-select>
      <template #footer><el-button @click="batchGroup.visible=false">取消</el-button><el-button type="primary" :loading="batchGroup.saving" @click="applyBatchGroup">保存</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ArrowDown } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { panelApi } from '@/api/panel'
import type { CustomerDetail, CustomerGroupOption, CustomerImportBatch, CustomerItem } from '@/api/types'
import { formatTime } from '@/utils/format'

const loading = ref(false)
const loadError = ref('')
const rows = ref<CustomerItem[]>([])
const total = ref(0)
const groups = ref<CustomerGroupOption[]>([])
const batches = ref<CustomerImportBatch[]>([])
const selectedIds = ref<number[]>([])
const customerTable = ref()
const filters = reactive({ page: 1, pageSize: 20, search: '', status: '', interaction: '', groupId: undefined as number | undefined, batchId: undefined as number | undefined })
const importDialog = reactive({ visible: false, saving: false, batchName: '', groupId: undefined as number | undefined, values: '' })
const detailDialog = reactive({ visible: false, loading: false, data: null as CustomerDetail | null })
const batchGroup = reactive({ visible: false, saving: false, groupId: undefined as number | undefined })

const statusLabel = (v: string) => ({ pending: '待查询', found: '已确认', not_found: '未确认', error: '查询异常' }[v] || v)
const statusType = (v: string) => (v === 'found' ? 'success' : v === 'pending' ? 'info' : 'warning')
const interactionLabel = (v: string) => (v === 'contacted' ? '已沟通' : '未执行')
const yesNo = (v: boolean) => v ? '有' : '无'
const activity = (r: CustomerItem | CustomerDetail) => {
  const statusMap: Record<string, string> = { online: '当前在线', offline: '离线', recently: '最近上线', last_week: '一周内上线', last_month: '一个月内上线', unknown: '状态未知/不可见' }
  const label = statusMap[r.activityStatus] || '状态未知/不可见'
  return r.lastSeenAt ? `${label}（${formatTime(r.lastSeenAt)}）` : label
}

async function loadCustomers() {
  loading.value = true
  try {
    const r = await panelApi.customers({ ...filters })
    rows.value = r.items
    total.value = r.total
    loadError.value = ''
  } catch {
    loadError.value = '客户数据加载失败，已保留当前页面数据。'
  } finally {
    loading.value = false
  }
}
async function loadMeta() {
  const [g, b] = await Promise.allSettled([panelApi.customerGroups(), panelApi.customerImportBatches()])
  if (g.status === 'fulfilled') groups.value = g.value
  if (b.status === 'fulfilled') batches.value = b.value
}
function loadAll() { loadCustomers(); loadMeta() }
function onSelection(s: CustomerItem[]) { selectedIds.value = s.map(x => x.id) }
function selectCurrentPage() { rows.value.forEach(row => customerTable.value?.toggleRowSelection(row, true)) }
function clearSelection() { customerTable.value?.clearSelection() }
function search() { filters.page = 1; loadCustomers() }
function reset() { Object.assign(filters, { page: 1, search: '', status: '', interaction: '', groupId: undefined, batchId: undefined }); loadCustomers() }
function requireCurrentGroup() {
  if (!filters.groupId) { ElMessage.warning('请先在筛选栏选择一个客户分类'); return false }
  return true
}
function selectBy(predicate: (row: CustomerItem) => boolean) {
  clearSelection()
  rows.value.filter(predicate).forEach(row => customerTable.value?.toggleRowSelection(row, true))
}
async function handleBatchCommand(command: string) {
  if (command === 'select-unexecuted-page') { selectBy(row => row.interactionStatus !== 'contacted'); return }
  if (command === 'select-group-contacted') { if (!requireCurrentGroup()) return; selectBy(row => row.interactionStatus === 'contacted'); return }
  if (command === 'select-group-unexecuted') { if (!requireCurrentGroup()) return; selectBy(row => row.interactionStatus !== 'contacted'); return }
  if (command === 'mark-contacted') return markSelected('contacted')
  if (command === 'mark-unexecuted') return markSelected('uncontacted')
}
async function markSelected(status: 'contacted' | 'uncontacted') {
  if (!selectedIds.value.length) return
  await panelApi.batchCustomers(selectedIds.value, 'set_interaction', undefined, status)
  ElMessage.success(status === 'contacted' ? '已标记为已沟通' : '已标记为未执行')
  await loadCustomers()
}
async function submitImport() {
  if (!importDialog.values.trim()) return ElMessage.warning('请填写客户')
  importDialog.saving = true
  try {
    const r = await panelApi.importCustomers({ values: importDialog.values, batchName: importDialog.batchName || undefined, groupId: importDialog.groupId })
    ElMessage.success(`导入 ${r.imported}，重复 ${r.duplicates}，无效 ${r.invalid}`)
    importDialog.visible = false
    importDialog.values = ''
    await loadAll()
  } finally { importDialog.saving = false }
}
async function showDetail(id: number) {
  detailDialog.visible = true
  detailDialog.loading = true
  try { detailDialog.data = await panelApi.customer(id) } finally { detailDialog.loading = false }
}
async function remove(row: CustomerItem) {
  await ElMessageBox.confirm(`删除客户 #${row.id}？`, '确认删除', { type: 'warning' })
  await panelApi.deleteCustomer(row.id)
  await loadCustomers()
}
function openBatchGroup() { batchGroup.groupId = undefined; batchGroup.visible = true }
async function applyBatchGroup() {
  batchGroup.saving = true
  try {
    await panelApi.batchCustomers(selectedIds.value, 'set_group', batchGroup.groupId ?? null)
    batchGroup.visible = false
    await loadCustomers()
  } finally { batchGroup.saving = false }
}
async function batchDelete() {
  await ElMessageBox.confirm(`删除已选 ${selectedIds.value.length} 个客户？`, '批量删除', { type: 'warning' })
  await panelApi.batchCustomers(selectedIds.value, 'delete')
  selectedIds.value = []
  await loadCustomers()
}
onMounted(loadAll)
</script>
<style scoped>
.customers-page{min-width:0;overflow:hidden}
.filter-row,.action-bar{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.filter{width:170px}.search{flex:1;min-width:260px}
.action-card{margin:14px auto}
.action-bar{width:100%;box-sizing:border-box}
.action-bar :deep(.el-button){margin-left:0;flex:0 0 auto}
.pager{display:flex;justify-content:flex-end;align-items:center;gap:18px;margin-top:16px}
.full{width:100%}.dialog-form{margin-top:16px}.tag{margin-right:5px}.muted{color:var(--tp-muted)}.mb{margin-bottom:14px}
.row-actions{display:flex;justify-content:flex-end;gap:2px}
.customers-table{width:100%;max-width:100%}
.customers-table :deep(.el-table-fixed-column--right),.customers-table :deep(.el-table__fixed-right-patch){background:var(--tp-panel)!important;z-index:6!important}
.customers-table :deep(.el-table__body tr:hover>.el-table-fixed-column--right){background:var(--tp-table-row-hover-bg)!important}
.customers-table :deep(.el-table__inner-wrapper::after){content:'';position:absolute;z-index:5;top:0;right:0;bottom:12px;width:160px;pointer-events:none;background:var(--tp-panel)}
@media(max-width:760px){.filter,.search{width:100%;min-width:100%}.action-bar{flex-wrap:nowrap;overflow-x:auto;padding-bottom:6px}}
</style>

