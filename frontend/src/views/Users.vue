<template>
  <div class="users-page">
    <section class="users-hero">
      <div>
        <div class="eyebrow">ACCESS CONTROL</div>
        <h1>团队与权限</h1>
        <p>为团队成员分配明确角色。管理员负责配置，运营人员执行任务，审计员只读查看。</p>
      </div>
      <el-button type="primary" size="large" :icon="Plus" @click="openCreate">添加成员</el-button>
    </section>

    <div class="role-grid">
      <article v-for="role in roleCards" :key="role.value" :class="['role-card', `role-${role.value}`]">
        <div class="role-icon"><span class="material-icons">{{ role.icon }}</span></div>
        <div>
          <div class="role-title">{{ role.label }}</div>
          <div class="role-description">{{ role.description }}</div>
        </div>
        <strong>{{ roleCount(role.value) }}</strong>
      </article>
    </div>

    <el-card shadow="never" class="users-table-card">
      <template #header>
        <div class="table-heading">
          <div>
            <strong>成员列表</strong>
            <span>共 {{ users.length }} 位成员</span>
          </div>
          <el-button :icon="Refresh" circle title="刷新" :loading="loading" @click="load" />
        </div>
      </template>

      <el-table v-loading="loading" :data="users" row-key="username">
        <el-table-column label="成员" min-width="240">
          <template #default="{ row }">
            <div class="member-cell">
              <el-avatar :size="38" :class="`avatar-${row.role}`">{{ row.username[0]?.toUpperCase() }}</el-avatar>
              <div>
                <div class="member-name">
                  {{ row.username }}
                  <el-tag v-if="row.username === auth.me?.username" size="small" effect="plain">当前用户</el-tag>
                </div>
                <span>{{ formatTime(row.createdAtUtc) }} 创建</span>
              </div>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="角色" width="150">
          <template #default="{ row }">
            <el-tag :type="roleType(row.role)" effect="light" round>{{ roleLabel(row.role) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="120">
          <template #default="{ row }">
            <div class="status-cell"><span :class="['status-dot', row.enabled ? 'online' : 'offline']" />{{ row.enabled ? '已启用' : '已停用' }}</div>
          </template>
        </el-table-column>
        <el-table-column label="密码" width="140">
          <template #default="{ row }">
            <span :class="row.mustChangePassword ? 'pending-password' : 'muted'">
              {{ row.mustChangePassword ? '等待首次修改' : '已设置' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="210" align="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
            <el-button link @click="openReset(row)">重置密码</el-button>
            <el-button
              link
              type="danger"
              :disabled="row.username === auth.me?.username"
              @click="remove(row)"
            >删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="createDialog" title="添加团队成员" width="480px" destroy-on-close>
      <el-form label-position="top">
        <el-form-item label="用户名">
          <el-input v-model="createForm.username" maxlength="32" placeholder="4-32位字母、数字或 _ - ." />
        </el-form-item>
        <el-form-item label="初始密码">
          <el-input v-model="createForm.password" type="password" show-password maxlength="128" placeholder="至少6位" />
        </el-form-item>
        <el-form-item label="角色">
          <el-radio-group v-model="createForm.role" class="role-selector">
            <el-radio-button v-for="item in roleCards" :key="item.value" :value="item.value">
              {{ item.label }}
            </el-radio-button>
          </el-radio-group>
          <div class="form-help">{{ roleDescription(createForm.role) }}</div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="createDialog = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="create">创建成员</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="editDialog" title="编辑成员权限" width="460px" destroy-on-close>
      <div class="edit-member" v-if="selected">
        <el-avatar :size="44" :class="`avatar-${selected.role}`">{{ selected.username[0]?.toUpperCase() }}</el-avatar>
        <div><strong>{{ selected.username }}</strong><span>修改后将自动刷新其登录权限</span></div>
      </div>
      <el-form label-position="top">
        <el-form-item label="角色">
          <el-select v-model="editForm.role" class="w-full">
            <el-option v-for="item in roleCards" :key="item.value" :label="item.label" :value="item.value" />
          </el-select>
          <div class="form-help">{{ roleDescription(editForm.role) }}</div>
        </el-form-item>
        <el-form-item label="左侧导航栏功能">
          <el-checkbox-group v-model="editForm.navigationItems" class="navigation-permission-grid">
            <el-checkbox v-for="item in navigationOptions" :key="item.value" :value="item.value">{{ item.label }}</el-checkbox>
          </el-checkbox-group>
          <div class="form-help">仅控制该成员登录后显示的导航入口；接口操作权限仍由角色控制。留空表示不显示业务导航。</div>
        </el-form-item>
        <el-form-item label="账号状态">
          <el-switch v-model="editForm.enabled" active-text="启用" inactive-text="停用" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editDialog = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="saveEdit">保存设置</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="resetDialog" title="重置成员密码" width="440px" destroy-on-close>
      <el-alert type="info" :closable="false" show-icon :title="`为 ${selected?.username || ''} 设置新的临时密码`" />
      <el-form label-position="top" class="mt-3">
        <el-form-item label="新密码">
          <el-input v-model="newPassword" type="password" show-password maxlength="128" placeholder="至少6位" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="resetDialog = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="resetPassword">确认重置</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh } from '@element-plus/icons-vue'
import { panelApi } from '@/api/panel'
import type { PanelRole, PanelUser } from '@/api/types'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const users = ref<PanelUser[]>([])
const loading = ref(false)
const saving = ref(false)
const createDialog = ref(false)
const editDialog = ref(false)
const resetDialog = ref(false)
const selected = ref<PanelUser | null>(null)
const newPassword = ref('')
const createForm = reactive({ username: '', password: '', role: 'operator' as PanelRole })
const editForm = reactive({ role: 'operator' as PanelRole, enabled: true, navigationItems: [] as string[] })


const navigationOptions = [
  { value: '/dashboard', label: '仪表盘' },
  { value: 'accounts-group', label: '账号管理' },
  { value: 'customers-group', label: '客户管理' },
  { value: '/customers', label: '客户列表' },
  { value: '/customers/categories', label: '客户分类' },
  { value: '/proxies', label: '代理管理' },
  { value: 'channels-group', label: '频道管理' },
  { value: 'groups-group', label: '群组管理' },
  { value: 'bots-group', label: '机器人管理' },
  { value: '/tasks', label: '任务中心' },
  { value: '/data-dictionaries', label: '数据字典' },
  { value: '/modules', label: '模块管理' },
  { value: '/apis', label: 'API 管理' },
  { value: '/device-profiles', label: '设备指纹' },
  { value: '/settings', label: '系统设置' },
]
const defaultNavigationItems = navigationOptions.map((item) => item.value)

const roleCards: Array<{ value: PanelRole; label: string; icon: string; description: string }> = [
  { value: 'admin', label: '管理员', icon: 'admin_panel_settings', description: '完整系统配置、成员和业务管理权限' },
  { value: 'operator', label: '运营人员', icon: 'rocket_launch', description: '管理账号、群组和任务，不可修改系统核心配置' },
  { value: 'auditor', label: '只读审计', icon: 'visibility', description: '查看仪表盘、资源和执行记录，不可提交修改' },
]

const activeUsers = computed(() => users.value.filter((item) => item.enabled).length)
void activeUsers

async function load() {
  loading.value = true
  try {
    users.value = await panelApi.users()
  } finally {
    loading.value = false
  }
}

function openCreate() {
  Object.assign(createForm, { username: '', password: '', role: 'operator' })
  createDialog.value = true
}

async function create() {
  if (createForm.username.trim().length < 4) return ElMessage.warning('用户名至少4位')
  if (createForm.password.trim().length < 6) return ElMessage.warning('密码至少6位')
  saving.value = true
  try {
    await panelApi.createUser({ ...createForm, username: createForm.username.trim(), password: createForm.password.trim() })
    ElMessage.success('成员已创建')
    createDialog.value = false
    await load()
  } finally {
    saving.value = false
  }
}

function openEdit(user: PanelUser) {
  selected.value = user
  Object.assign(editForm, { role: user.role, enabled: user.enabled, navigationItems: user.navigationItems == null ? [...defaultNavigationItems] : [...user.navigationItems] })
  editDialog.value = true
}

async function saveEdit() {
  if (!selected.value) return
  saving.value = true
  try {
    await panelApi.updateUser(selected.value.username, editForm)
    ElMessage.success('成员权限已更新')
    editDialog.value = false
    await load()
  } finally {
    saving.value = false
  }
}

function openReset(user: PanelUser) {
  selected.value = user
  newPassword.value = ''
  resetDialog.value = true
}

async function resetPassword() {
  if (!selected.value) return
  if (newPassword.value.trim().length < 6) return ElMessage.warning('密码至少6位')
  saving.value = true
  try {
    await panelApi.resetUserPassword(selected.value.username, newPassword.value.trim())
    ElMessage.success('密码已重置')
    resetDialog.value = false
    await load()
  } finally {
    saving.value = false
  }
}

async function remove(user: PanelUser) {
  await ElMessageBox.confirm(`确认删除成员「${user.username}」？`, '删除成员', { type: 'warning' })
  await panelApi.deleteUser(user.username)
  ElMessage.success('成员已删除')
  await load()
}

function roleCount(role: PanelRole) {
  return users.value.filter((item) => item.role === role && item.enabled).length
}

function roleLabel(role: PanelRole) {
  return roleCards.find((item) => item.value === role)?.label || role
}

function roleDescription(role: PanelRole) {
  return roleCards.find((item) => item.value === role)?.description || ''
}

function roleType(role: PanelRole) {
  if (role === 'admin') return 'danger'
  if (role === 'operator') return 'primary'
  return 'info'
}

function formatTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  return new Intl.DateTimeFormat('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' }).format(date)
}

onMounted(load)
</script>

<style scoped>
.users-page { display: grid; gap: 18px; }
.users-hero { display:flex; align-items:center; justify-content:space-between; gap:24px; padding:28px 30px; border-radius:20px; color:#fff; background:linear-gradient(125deg,#172554 0%,#1d4ed8 58%,#06b6d4 120%); box-shadow:0 18px 45px rgba(30,64,175,.2); }
.eyebrow { font-size:11px; font-weight:800; letter-spacing:.18em; color:#a5f3fc; }
.users-hero h1 { margin:7px 0 8px; font-size:29px; line-height:1.15; }
.users-hero p { margin:0; max-width:650px; color:rgba(255,255,255,.72); }
.users-hero :deep(.el-button) { border:0; color:#1e3a8a; background:#fff; font-weight:700; box-shadow:0 8px 24px rgba(15,23,42,.2); }
.role-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:14px; }
.role-card { display:grid; grid-template-columns:auto 1fr auto; align-items:center; gap:14px; padding:18px; border:1px solid var(--tp-border); border-radius:16px; background:var(--tp-panel); box-shadow:var(--tp-card-shadow); }
.role-icon { display:grid; place-items:center; width:42px; height:42px; border-radius:13px; }
.role-admin .role-icon { color:#dc2626; background:#fef2f2; }
.role-operator .role-icon { color:#2563eb; background:#eff6ff; }
.role-auditor .role-icon { color:#64748b; background:#f1f5f9; }
.role-title { font-weight:750; color:var(--tp-text); }
.role-description { margin-top:4px; color:var(--tp-muted); font-size:12px; line-height:1.45; }
.role-card > strong { font-size:26px; color:var(--tp-text); }
.users-table-card { border-radius:16px; border-color:var(--tp-border); }
.table-heading { display:flex; justify-content:space-between; align-items:center; }
.table-heading > div { display:flex; align-items:baseline; gap:10px; }
.table-heading strong { font-size:16px; }
.table-heading span { color:var(--tp-muted); font-size:12px; }
.member-cell,.edit-member { display:flex; align-items:center; gap:12px; }
.member-cell > div,.edit-member > div { display:grid; gap:3px; }
.member-cell span,.edit-member span { color:var(--tp-muted); font-size:12px; }
.member-name { display:flex; align-items:center; gap:8px; font-weight:650; }
.avatar-admin { color:#991b1b; background:#fee2e2; }
.avatar-operator { color:#1d4ed8; background:#dbeafe; }
.avatar-auditor { color:#475569; background:#e2e8f0; }
.status-cell { display:flex; align-items:center; gap:8px; }
.status-dot { width:8px; height:8px; border-radius:50%; }
.status-dot.online { background:#22c55e; box-shadow:0 0 0 4px #dcfce7; }
.status-dot.offline { background:#94a3b8; }
.pending-password { color:#d97706; font-size:12px; }
.muted,.form-help { color:var(--tp-muted); font-size:12px; }
.form-help { margin-top:8px; line-height:1.5; }
.role-selector,.w-full { width:100%; }
.role-selector :deep(.el-radio-button) { flex:1; }
.role-selector :deep(.el-radio-button__inner) { width:100%; }
.edit-member { padding:14px; margin-bottom:18px; border-radius:12px; background:var(--tp-surface); }
@media (max-width:900px) { .role-grid { grid-template-columns:1fr; } .users-hero { align-items:flex-start; flex-direction:column; } }
.navigation-permission-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  width: 100%;
  gap: 6px 12px;
}

</style>
