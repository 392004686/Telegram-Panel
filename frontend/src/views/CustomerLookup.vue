<template>
  <div class="lookup-page">
    <el-alert v-if="loadError" :title="loadError" type="error" show-icon :closable="false" class="mb"/>
    <el-alert class="mb" type="info" :closable="false" title="查询手机号通常不会返回真实姓名（Telegram 会给临时联系人占位名）；查 @用户名/用户ID 才更容易拿到公开名称。这是正常现象。"/>
    <el-card shadow="never" class="page-card"><el-form label-position="top">
      <el-form-item label="查询目标"><el-input v-model="form.targets" type="textarea" :rows="6" placeholder="单个或每行一个：+1 123 456 7890、1234567890、@username"/></el-form-item>
      <div class="grid"><el-form-item label="账号来源"><el-radio-group v-model="form.source"><el-radio-button value="account">指定账号（多选）</el-radio-button><el-radio-button value="category">账号分类轮询</el-radio-button></el-radio-group></el-form-item><el-form-item v-if="form.source==='category'" label="账号分类"><el-select v-model="form.categoryId" class="full"><el-option v-for="c in categories" :key="c.id" :label="`${c.name}（${c.accountCount}）`" :value="c.id"/></el-select></el-form-item></div>
      <div v-if="form.source==='account'" class="account-picker"><div class="picker-actions"><el-button size="small" @click="selectAllAccounts">全选本页</el-button><el-button size="small" @click="clearAccounts">取消全选</el-button><span class="muted">已选 {{selectedAccountIds.length}} 个执行账号</span></div>
        <el-table ref="accountTable" :data="usableAccounts" row-key="id" size="small" class="fixed-table" @selection-change="onAccountSelection"><el-table-column type="selection" width="48" reserve-selection/><el-table-column prop="displayPhone" label="手机号" min-width="150"/><el-table-column label="昵称" min-width="130"><template #default="{row}">{{row.nickname||row.remark||'-'}}</template></el-table-column><el-table-column label="当前分类" min-width="130"><template #default="{row}">{{row.category?.name||'未分类'}}</template></el-table-column><el-table-column label="用户名" min-width="130"><template #default="{row}">{{row.username?'@'+row.username:'-'}}</template></el-table-column><el-table-column label="Telegram 状态" min-width="150"><template #default="{row}">{{row.telegramStatusSummary||'未检测'}}</template></el-table-column><el-table-column label="操作" fixed="right" width="100"><template #default="{row}"><el-button link type="danger" @click="removeSelectedAccount(row)">移出选择</el-button></template></el-table-column></el-table>
      </div>
      <div class="grid three"><el-form-item label="最小间隔（秒）"><el-input-number v-model="form.minDelay" :min="0" :max="3600"/></el-form-item><el-form-item label="最大间隔（秒）"><el-input-number v-model="form.maxDelay" :min="form.minDelay" :max="3600"/></el-form-item><el-form-item label="目标模式"><el-radio-group v-model="form.order"><el-radio-button value="queue">队列</el-radio-button><el-radio-button value="random">随机</el-radio-button></el-radio-group></el-form-item></div>
      <el-alert title="查询先持久化批次；关闭页面、切换路由或容器重启后仍可在任务中心跟踪。" type="success" :closable="false" class="mb"/><el-button type="primary" :loading="submitting" @click="start('realtime')">实时查询</el-button><el-button :loading="submitting" @click="start('task')">发送任务</el-button>
    </el-form></el-card>
    <el-card shadow="never" class="page-card mt-4"><template #header><div class="header"><b>当前查询结果</b><span v-if="currentBatch">{{currentBatch.completed}} / {{currentBatch.total}}，找到 {{currentBatch.found}}，失败 {{currentBatch.failed}}</span></div></template>
      <el-table :data="results" stripe empty-text="请选择历史批次或创建查询" class="fixed-table"><el-table-column prop="rawTarget" label="查询目标" min-width="145"/><el-table-column label="结果" width="105"><template #default="{row}"><el-tag :type="row.status==='found'?'success':row.status==='failed'?'danger':'warning'">{{statusLabel(row.status)}}</el-tag></template></el-table-column><el-table-column label="客户列表" width="105"><template #default="{row}">{{row.existingCustomer?'已更新':'未收录'}}</template></el-table-column><el-table-column prop="telegramUserId" label="用户ID" min-width="125"/><el-table-column prop="displayName" label="姓名" min-width="120"/><el-table-column label="用户名" min-width="130"><template #default="{row}">{{row.username?'@'+row.username:'-'}}</template></el-table-column><el-table-column prop="phone" label="手机号" min-width="140"/><el-table-column label="活跃状态" min-width="180"><template #default="{row}">{{activity(row)}}</template></el-table-column><el-table-column prop="error" label="说明" min-width="180"/><el-table-column label="操作" width="90" fixed="right"><template #default><span class="muted">明细</span></template></el-table-column></el-table>
    </el-card>
    <el-card shadow="never" class="page-card mt-4">
      <template #header>
        <div class="header">
          <b>历史查询批次</b>
          <div class="header-actions">
            <el-input v-model="historyFilter.search" clearable size="small" placeholder="模糊搜索名称/查询内容" style="width:240px" @keyup.enter="searchHistory"/>
            <el-select v-model="historyFilter.status" clearable placeholder="全部状态" size="small" style="width:130px" @change="searchHistory">
              <el-option label="等待" value="pending"/><el-option label="查询中" value="running"/><el-option label="已完成" value="completed"/><el-option label="已中断" value="interrupted"/>
            </el-select>
            <el-button size="small" @click="searchHistory">查询</el-button>
            <el-button size="small" @click="selectHistoryPage">全选本页</el-button>
            <el-button size="small" @click="clearHistorySelection">取消全选</el-button>
            <el-button size="small" @click="clearHistorySelection">取消勾选</el-button>
            <el-button size="small" type="danger" plain :disabled="!selectedHistoryIds.length" @click="batchDeleteHistory">批量删除</el-button>
            <el-button size="small" @click="loadHistory">刷新</el-button>
          </div>
        </div>
      </template>
      <el-table ref="historyTable" :data="history" row-key="id" class="fixed-table" @selection-change="onHistorySelection">
        <el-table-column type="selection" width="48" reserve-selection :selectable="(row: CustomerLookupBatch) => !active(row)"/>
        <el-table-column prop="name" label="名称" min-width="160"/>
        <el-table-column label="查询内容" min-width="240"><template #default="{row}"><span class="query-preview" :title="row.queryPreview || ''">{{row.queryPreview || '-'}}</span></template></el-table-column>
        <el-table-column label="状态" width="100"><template #default="{row}">{{statusLabel(row.status)}}</template></el-table-column>
        <el-table-column label="进度" width="130"><template #default="{row}">{{row.completed}} / {{row.total}}</template></el-table-column>
        <el-table-column label="创建时间" min-width="170"><template #default="{row}">{{formatTime(row.createdAt)}}</template></el-table-column>
        <el-table-column label="操作" width="210" fixed="right"><template #default="{row}"><el-button link type="primary" @click="openBatch(row.id)">查看</el-button><el-button link type="warning" :disabled="active(row)" @click="retryBatch(row.id)">失败重试</el-button><el-button link type="danger" :disabled="active(row)" @click="deleteBatch(row.id)">删除记录</el-button></template></el-table-column>
      </el-table>
      <div class="pager"><span>已选 {{selectedHistoryIds.length}} / 共 {{historyTotal}} 条</span><el-pagination v-model:current-page="historyFilter.page" v-model:page-size="historyFilter.pageSize" layout="sizes, prev, pager, next" :total="historyTotal" :page-sizes="[10,20,50]" @change="loadHistory"/></div>
    </el-card>
  </div>
</template>
<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { panelApi } from '@/api/panel'
import type { AccountCategory, AccountListItem, CustomerLookupBatch, CustomerLookupItem } from '@/api/types'
import { formatTime } from '@/utils/format'
const accounts = ref<AccountListItem[]>([])
const categories = ref<AccountCategory[]>([])
const selectedAccountIds = ref<number[]>([])
const accountTable = ref()
const historyTable = ref()
const history = ref<CustomerLookupBatch[]>([])
const historyTotal = ref(0)
const selectedHistoryIds = ref<number[]>([])
const results = ref<CustomerLookupItem[]>([])
const currentBatch = ref<CustomerLookupBatch | null>(null)
const submitting = ref(false)
const loadError = ref('')
const form = reactive({ targets: '', source: 'account', categoryId: undefined as number | undefined, minDelay: 3, maxDelay: 8, order: 'queue' })
const historyFilter = reactive({ status: '', search: '', page: 1, pageSize: 10 })
let timer: number | undefined
const usableAccounts = computed(() => accounts.value.filter(a => a.isActive && a.telegramStatusOk !== false))
const targets = computed(() => form.targets.split(/[\r\n,;]+/).map(x => x.trim()).filter(Boolean))
const active = (b: CustomerLookupBatch) => ['pending', 'running'].includes(b.status)
const statusLabel = (v: string) => ({ pending: '等待', running: '查询中', completed: '已完成', interrupted: '已中断', found: '存在', not_found: '未找到', failed: '异常', error: '查询异常' }[v] || '未知状态')
const activity = (r: CustomerLookupItem) => {
  const label = ({ online: '当前在线', offline: '离线', recently: '最近上线', last_week: '一周内上线', last_month: '一个月内上线', unknown: '状态未知/不可见' } as Record<string, string>)[r.activityStatus] || '状态未知/不可见'
  return r.lastSeenAt ? `${label}（${formatTime(r.lastSeenAt)}）` : label
}
async function loadOptions() {
  const rs = await Promise.allSettled([panelApi.accounts({ page: 1, pageSize: 500, categoryId: null, search: '', onlyWaste: false }), panelApi.accountCategories()])
  if (rs[0].status === 'fulfilled') accounts.value = rs[0].value.items
  if (rs[1].status === 'fulfilled') categories.value = rs[1].value
  loadError.value = rs.some(x => x.status === 'rejected') ? '部分账号选项加载失败。' : ''
}
function onAccountSelection(rows: AccountListItem[]) { selectedAccountIds.value = rows.map(x => x.id) }
function selectAllAccounts() { usableAccounts.value.forEach(x => accountTable.value?.toggleRowSelection(x, true)) }
function clearAccounts() { accountTable.value?.clearSelection() }
function removeSelectedAccount(row: AccountListItem) { accountTable.value?.toggleRowSelection(row, false) }
function onHistorySelection(rows: CustomerLookupBatch[]) { selectedHistoryIds.value = rows.map(x => x.id) }
function selectHistoryPage() { history.value.filter(x => !active(x)).forEach(row => historyTable.value?.toggleRowSelection(row, true)) }
function clearHistorySelection() { historyTable.value?.clearSelection() }
function searchHistory() { historyFilter.page = 1; loadHistory() }
async function start(mode: string) {
  if (!targets.value.length) return ElMessage.warning('请输入查询目标')
  if (form.source === 'account' && !selectedAccountIds.value.length) return ElMessage.warning('请选择至少一个执行账号')
  if (form.source === 'category' && !form.categoryId) return ElMessage.warning('请选择账号分类')
  submitting.value = true
  try {
    const r = await panelApi.createCustomerLookupBatch({ mode, targets: targets.value, accountIds: form.source === 'account' ? selectedAccountIds.value : [], accountCategoryId: form.source === 'category' ? form.categoryId : null, targetOrder: form.order, minDelaySeconds: form.minDelay, maxDelaySeconds: form.maxDelay })
    ElMessage.success(mode === 'realtime' ? '实时查询已完成，结果已保存到历史查询' : `任务 #${r.taskId} 已进入任务中心`)
    await loadHistory()
    await openBatch(r.batchId)
  } finally { submitting.value = false }
}
async function loadHistory() {
  const r = await panelApi.customerLookupBatches({ page: historyFilter.page, pageSize: historyFilter.pageSize, status: historyFilter.status || undefined, search: historyFilter.search || undefined })
  history.value = r.items
  historyTotal.value = r.total
}
async function openBatch(id: number) {
  const r = await panelApi.customerLookupBatch(id)
  currentBatch.value = r.batch
  results.value = r.items
  if (timer) clearInterval(timer)
  if (active(r.batch)) timer = window.setInterval(() => refreshCurrent(id), 2000)
}
async function refreshCurrent(id: number) {
  const r = await panelApi.customerLookupBatch(id)
  currentBatch.value = r.batch
  results.value = r.items
  if (!active(r.batch)) { if (timer) clearInterval(timer); timer = undefined; await loadHistory() }
}
async function retryBatch(id: number) {
  const r = await panelApi.retryCustomerLookupBatch(id)
  ElMessage.success(`重试任务 #${r.taskId} 已创建`)
  await openBatch(id)
}
async function deleteBatch(id: number) {
  await ElMessageBox.confirm('删除该查询批次及全部明细？', '确认删除', { type: 'warning' })
  await panelApi.deleteCustomerLookupBatch(id)
  if (currentBatch.value?.id === id) { currentBatch.value = null; results.value = [] }
  await loadHistory()
}
async function batchDeleteHistory() {
  if (!selectedHistoryIds.value.length) return
  await ElMessageBox.confirm(`删除已选 ${selectedHistoryIds.value.length} 个历史批次？`, '批量删除', { type: 'warning' })
  await panelApi.batchDeleteCustomerLookupBatches(selectedHistoryIds.value)
  selectedHistoryIds.value = []
  historyTable.value?.clearSelection()
  await loadHistory()
}
onMounted(() => Promise.all([loadOptions(), loadHistory()]))
onBeforeUnmount(() => { if (timer) clearInterval(timer) })
</script>
<style scoped>
.lookup-page{min-width:0}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.grid.three{grid-template-columns:repeat(3,minmax(0,1fr))}.full{width:100%}.mb{margin-bottom:14px}
.header,.picker-actions{display:flex;justify-content:space-between;align-items:center;gap:10px;flex-wrap:wrap}
.header-actions{display:flex;align-items:center;gap:8px;flex-wrap:wrap}
.account-picker{margin-bottom:16px}.picker-actions{justify-content:flex-start;margin-bottom:8px}.muted{color:var(--tp-muted)}
.query-preview{display:block;max-width:100%;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.pager{display:flex;justify-content:flex-end;align-items:center;gap:18px;margin-top:16px}
.fixed-table :deep(.el-table-fixed-column--right),.fixed-table :deep(.el-table__fixed-right-patch){background:var(--tp-panel)!important;z-index:5!important}
.fixed-table :deep(.el-table__body tr:hover>.el-table-fixed-column--right){background:var(--tp-table-row-hover-bg)!important}
@media(max-width:760px){.grid,.grid.three{grid-template-columns:1fr}}
</style>

