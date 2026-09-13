<template>
  <div class="dashboard-page">
    <section class="overview-hero">
      <div class="hero-copy">
        <div class="hero-kicker">{{ greeting }}，{{ auth.me?.username || '管理员' }}</div>
        <h2>今天的运营状态一目了然</h2>
        <p>账号、资源与任务数据已汇总到一个工作台，异常状态会在这里优先呈现。</p>
        <div class="hero-actions" v-if="auth.canOperate">
          <el-button type="primary" :icon="Upload" @click="router.push('/accounts/import')">导入账号</el-button>
          <el-button :icon="Refresh" :loading="syncing" @click="syncAll">立即同步</el-button>
        </div>
      </div>
      <div class="hero-health">
        <div class="health-ring" :style="{ '--health': `${accountHealth}%` }"><span>{{ accountHealth }}%</span><small>账号健康度</small></div>
        <div class="health-caption"><span class="live-dot" />实时运行中</div>
      </div>
    </section>

    <section class="metric-grid">
      <article v-for="item in metrics" :key="item.label" class="metric-card">
        <div :class="['metric-icon', item.tone]"><span class="material-icons">{{ item.icon }}</span></div>
        <div class="metric-content"><span>{{ item.label }}</span><strong>{{ item.value }}</strong><small>{{ item.note }}</small></div>
      </article>
    </section>

    <section class="dashboard-grid">
      <el-card shadow="never" class="panel-card task-panel">
        <template #header>
          <div class="panel-heading">
            <div><strong>最近任务</strong><span>最新执行状态与进度</span></div>
            <el-button link type="primary" @click="router.push('/tasks')">查看全部 <span class="material-icons">arrow_forward</span></el-button>
          </div>
        </template>
        <div v-if="recentTaskRows.length" class="task-list" v-loading="loading && !summary">
          <article v-for="task in recentTaskRows.slice(0, 6)" :key="task.id" class="task-row">
            <div class="task-symbol"><span class="material-icons">bolt</span></div>
            <div class="task-main">
              <div><strong>{{ taskName(task) }}</strong><StatusTag :status="displayStatus(task)" /></div>
              <span>{{ fallbackTaskName(task.taskType) }} · {{ formatRecentTime(task.createdAt) }}</span>
              <el-progress :percentage="taskProgress(task)" :stroke-width="6" :show-text="false" />
            </div>
            <strong class="task-percent">{{ taskProgress(task) }}%</strong>
          </article>
        </div>
        <el-empty v-else description="还没有执行记录" :image-size="80" />
      </el-card>

      <div class="side-panels">
        <el-card shadow="never" class="panel-card system-card">
          <template #header><div class="panel-heading"><div><strong>系统状态</strong><span>服务与出口检测</span></div><el-button circle :icon="Refresh" :loading="egressLoading" @click="loadEgress" /></div></template>
          <div class="system-status">
            <div class="system-icon"><span class="material-icons">dns</span></div>
            <div><strong>{{ egress?.success ? '网络连接正常' : egressError ? '网络检测异常' : '正在检测网络' }}</strong><span>{{ egressDescription }}</span></div>
            <el-tag :type="egress?.success ? 'success' : 'warning'" effect="light" round>{{ egress?.success ? '在线' : (egress || egressError) ? '检测失败' : '检测中' }}</el-tag>
          </div>
          <div v-if="egress && !egress.success" class="egress-note">这里只表示面板公网出口检测结果，不代表代理管理中的独立 WARP 失效。</div>
          <div class="status-matrix">
            <div><span class="dot success" /><span>正常账号</span><strong>{{ summary?.normalAccountCount ?? '-' }}</strong></div>
            <div><span class="dot warning" /><span>受限账号</span><strong>{{ summary?.limitedAccountCount ?? '-' }}</strong></div>
            <div><span class="dot danger" /><span>失效账号</span><strong>{{ summary?.invalidAccountCount ?? '-' }}</strong></div>
          </div>
        </el-card>

        <el-card shadow="never" class="panel-card shortcut-card">
          <template #header><div class="panel-heading"><div><strong>快捷入口</strong><span>常用管理功能</span></div></div></template>
          <div class="shortcut-grid">
            <button @click="router.push('/accounts')"><span class="material-icons">group</span><b>账号中心</b><small>查看账号状态</small></button>
            <button @click="router.push('/groups')"><span class="material-icons">forum</span><b>群组资源</b><small>管理目标群组</small></button>
            <button @click="router.push('/tasks')"><span class="material-icons">schedule</span><b>任务中心</b><small>查看执行进度</small></button>
            <button v-if="auth.isAdmin" @click="router.push('/users')"><span class="material-icons">manage_accounts</span><b>团队权限</b><small>管理成员角色</small></button>
          </div>
        </el-card>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Refresh, Upload } from '@element-plus/icons-vue'
import { panelApi } from '@/api/panel'
import type { BatchTask, DashboardSummary, NetworkEgress } from '@/api/types'
import StatusTag from '@/components/StatusTag.vue'
import { taskProgress } from '@/utils/format'
import { ipVersionLabel } from '@/utils/networkEgress'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const auth = useAuthStore()
const loading = ref(false)
const syncing = ref(false)
const summary = ref<DashboardSummary | null>(null)
const egress = ref<NetworkEgress | null>(null)
const egressLoading = ref(false)
const egressError = ref('')
let timer: number | undefined
let loadPromise: Promise<void> | null = null

const greeting = computed(() => {
  const hour = new Date().getHours()
  if (hour < 6) return '夜深了'
  if (hour < 12) return '早上好'
  if (hour < 18) return '下午好'
  return '晚上好'
})
const accountHealth = computed(() => {
  const total = summary.value?.accountCount || 0
  if (!total) return 100
  return Math.round(((summary.value?.normalAccountCount || 0) / total) * 100)
})
const metrics = computed(() => [
  { label: '账号总数', value: summary.value?.accountCount ?? '-', note: `${summary.value?.normalAccountCount ?? 0} 个状态正常`, icon: 'people', tone: 'blue' },
  { label: '频道资源', value: summary.value?.channelCount ?? '-', note: '已同步频道', icon: 'campaign', tone: 'violet' },
  { label: '群组资源', value: summary.value?.groupCount ?? '-', note: '已同步群组', icon: 'forum', tone: 'cyan' },
  { label: '运行任务', value: summary.value?.activeTaskCount ?? '-', note: `${summary.value?.enabledScheduledTaskCount ?? 0} 个计划已启用`, icon: 'rocket_launch', tone: 'orange' },
])
const recentTaskRows = computed(() => [...(summary.value?.recentTasks || [])].sort((a, b) => new Date(b.completedAt || b.createdAt).getTime() - new Date(a.completedAt || a.createdAt).getTime()))
const needsAutoRefresh = computed(() => (summary.value?.activeTaskCount || 0) > 0 || (summary.value?.enabledScheduledTaskCount || 0) > 0)
const egressDescription = computed(() => {
  if (egressError.value) return egressError.value
  if (!egress.value) return '正在获取出口信息'
  if (!egress.value.success) return egress.value.error || '出口检测失败'
  const location = [egress.value.country, egress.value.city, egress.value.isp].filter(Boolean).join(' / ')
  return [egress.value.ip, ipVersionLabel(egress.value.ip), location].filter(Boolean).join(' · ') || '出口信息正常'
})

async function load(options: { silent?: boolean } = {}) {
  if (!options.silent) loading.value = true
  if (!loadPromise) loadPromise = panelApi.summary().then((value) => { summary.value = value }).finally(() => { loadPromise = null })
  try { await loadPromise } finally { if (!options.silent) loading.value = false }
}
async function syncAll() {
  if (syncing.value) return
  syncing.value = true
  try { const result = await panelApi.startSyncNow(); ElMessage.success(result.message || '同步完成'); await load() } finally { syncing.value = false }
}
async function loadEgress() {
  egressLoading.value = true; egressError.value = ''
  try { egress.value = await panelApi.networkEgress() } catch (error) { egress.value = null; egressError.value = error instanceof Error ? error.message : '网络检测失败' } finally { egressLoading.value = false }
}
function displayStatus(task: BatchTask) { return task.status === 'failed' && task.completedAt && task.total > 0 && task.completed >= task.total ? 'completed' : task.status }
function taskName(task: BatchTask) { return task.name?.trim() || `${fallbackTaskName(task.taskType)} #${task.id}` }
function fallbackTaskName(type: string) {
  const labels: Record<string, string> = { user_join_subscribe: '批量加群/订阅/启用Bot', bot_channel_set_admins_by_account: 'Bot频道设置管理员', bot_set_admins: 'Bot批量设置管理员', user_chat_active: '账号持续活跃', account_auto_sync: '账号数据同步', channel_group_private_create: '批量创建私密资源', channel_group_publicize: '批量公开资源', auto_change_login_email: '自动更改登录邮箱' }
  return labels[type] || type
}
function formatRecentTime(value?: string | null) { if (!value) return '-'; const date = new Date(value); return Number.isNaN(date.getTime()) ? '-' : new Intl.DateTimeFormat('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }).format(date) }

onMounted(() => { void load(); void loadEgress(); timer = window.setInterval(() => { if (document.visibilityState === 'visible' && needsAutoRefresh.value) void load({ silent: true }).catch(() => undefined) }, 12000) })
onUnmounted(() => { if (timer) window.clearInterval(timer) })
</script>

<style scoped>
.dashboard-page { display:grid; gap:18px; }
.overview-hero { position:relative; overflow:hidden; display:flex; align-items:center; justify-content:space-between; min-height:210px; padding:30px 36px; border-radius:20px; color:#fff; background:linear-gradient(120deg,#102454,#1d4ed8 58%,#0891b2); box-shadow:0 20px 50px rgba(30,64,175,.18); }
.overview-hero::after { content:""; position:absolute; width:340px; height:340px; right:-90px; top:-150px; border-radius:50%; background:rgba(255,255,255,.1); box-shadow:0 0 0 70px rgba(255,255,255,.04); }
.hero-copy,.hero-health { position:relative; z-index:1; }
.hero-kicker { color:#a5f3fc; font-size:13px; font-weight:750; }
.hero-copy h2 { margin:8px 0; font-size:31px; letter-spacing:-.035em; }
.hero-copy p { margin:0; color:rgba(255,255,255,.7); }
.hero-actions { display:flex; gap:10px; margin-top:24px; }
.hero-actions :deep(.el-button) { border-color:rgba(255,255,255,.2); }
.hero-actions :deep(.el-button--primary) { color:#1e3a8a; background:#fff; border-color:#fff; }
.hero-health { display:grid; justify-items:center; gap:12px; margin-right:28px; }
.health-ring { display:grid; place-items:center; width:122px; height:122px; border-radius:50%; background:radial-gradient(circle,#183a78 57%,transparent 58%),conic-gradient(#67e8f9 var(--health,100%),rgba(255,255,255,.16) 0); border:1px solid rgba(255,255,255,.12); }
.health-ring span { font-size:29px; font-weight:800; grid-area:1/1; }
.health-ring small { align-self:end; margin-bottom:32px; color:rgba(255,255,255,.6); font-size:10px; grid-area:1/1; transform:translateY(9px); }
.health-caption { display:flex; align-items:center; gap:8px; color:rgba(255,255,255,.7); font-size:12px; }
.live-dot { width:7px; height:7px; border-radius:50%; background:#34d399; box-shadow:0 0 0 4px rgba(52,211,153,.16); }
.metric-grid { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:14px; }
.metric-card { display:flex; align-items:center; gap:14px; padding:19px; border:1px solid var(--tp-border); border-radius:16px; background:var(--tp-panel); box-shadow:var(--tp-card-shadow); transition:transform .2s ease,box-shadow .2s ease; }
.metric-card:hover { transform:translateY(-2px); box-shadow:0 14px 35px rgba(15,23,42,.09); }
.metric-icon { display:grid; place-items:center; flex:0 0 46px; height:46px; border-radius:14px; }
.metric-icon.blue { color:#2563eb; background:#dbeafe; }.metric-icon.violet { color:#7c3aed; background:#ede9fe; }.metric-icon.cyan { color:#0891b2; background:#cffafe; }.metric-icon.orange { color:#ea580c; background:#ffedd5; }
.metric-content { display:grid; grid-template-columns:1fr auto; align-items:baseline; flex:1; }
.metric-content span { color:var(--tp-muted); font-size:12px; }.metric-content strong { grid-row:1/3; grid-column:2; font-size:27px; color:var(--tp-text); }.metric-content small { margin-top:5px; color:var(--tp-muted); }
.dashboard-grid { display:grid; grid-template-columns:minmax(0,1.5fr) minmax(330px,.8fr); gap:18px; align-items:start; }
.side-panels { display:grid; gap:18px; }.panel-card { border-radius:16px; border-color:var(--tp-border); background:var(--tp-panel); }
.panel-heading { display:flex; align-items:center; justify-content:space-between; }.panel-heading > div { display:grid; gap:3px; }.panel-heading strong { color:var(--tp-text); font-size:16px; }.panel-heading span { color:var(--tp-muted); font-size:11px; }.panel-heading .material-icons { font-size:16px; vertical-align:middle; }
.task-list { display:grid; }.task-row { display:flex; align-items:center; gap:13px; padding:14px 0; border-bottom:1px solid var(--tp-border); }.task-row:last-child { border-bottom:0; }
.task-symbol { display:grid; place-items:center; width:37px; height:37px; border-radius:12px; color:#2563eb; background:#eff6ff; }.task-symbol .material-icons { font-size:19px; }
.task-main { min-width:0; flex:1; display:grid; gap:6px; }.task-main > div { display:flex; align-items:center; gap:8px; }.task-main strong { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; color:var(--tp-text); font-size:13px; }.task-main > span { color:var(--tp-muted); font-size:11px; }.task-percent { color:#475569; font-size:12px; }
.system-status { display:grid; grid-template-columns:auto 1fr auto; align-items:center; gap:12px; padding-bottom:16px; }.system-icon { display:grid; place-items:center; width:40px; height:40px; border-radius:13px; color:#059669; background:#d1fae5; }.system-status > div:nth-child(2) { min-width:0; display:grid; gap:4px; }.system-status strong { color:var(--tp-text); font-size:13px; }.system-status span { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; color:var(--tp-muted); font-size:10px; }.egress-note { margin:0 0 14px; padding:9px 11px; border-radius:9px; color:#8a6a14; background:#fff8df; font-size:11px; line-height:1.5; } html.dark .egress-note { color:#f7d878; background:rgba(245,158,11,.1); }
.status-matrix { display:grid; grid-template-columns:repeat(3,1fr); padding-top:15px; border-top:1px solid var(--tp-border); }.status-matrix > div { display:grid; grid-template-columns:auto 1fr; gap:6px; padding:0 10px; border-right:1px solid var(--tp-border); }.status-matrix > div:last-child { border:0; }.status-matrix span:not(.dot) { color:var(--tp-muted); font-size:10px; }.status-matrix strong { grid-column:2; color:var(--tp-text); font-size:18px; }.dot { width:7px; height:7px; margin-top:3px; border-radius:50%; }.dot.success { background:#22c55e; }.dot.warning { background:#f59e0b; }.dot.danger { background:#ef4444; }
.shortcut-grid { display:grid; grid-template-columns:1fr 1fr; gap:10px; }.shortcut-grid button { display:grid; grid-template-columns:auto 1fr; gap:2px 9px; padding:13px; text-align:left; border:1px solid var(--tp-border); border-radius:12px; color:var(--tp-text); background:var(--tp-surface); cursor:pointer; transition:.2s ease; }.shortcut-grid button:hover { border-color:#93c5fd; background:#eff6ff; transform:translateY(-1px); }.shortcut-grid .material-icons { grid-row:1/3; color:#2563eb; font-size:21px; }.shortcut-grid b { font-size:12px; }.shortcut-grid small { color:var(--tp-muted); font-size:10px; }
@media (max-width:1100px) { .metric-grid { grid-template-columns:1fr 1fr; }.dashboard-grid { grid-template-columns:1fr; } }
@media (max-width:680px) { .overview-hero { padding:25px; }.hero-health { display:none; }.metric-grid { grid-template-columns:1fr; }.shortcut-grid { grid-template-columns:1fr; } }
</style>
