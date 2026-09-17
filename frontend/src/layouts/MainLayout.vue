<template>
  <el-container :class="['layout', { 'embed-layout': isEmbedMode }]">
    <el-header v-if="!isEmbedMode" class="appbar">
      <el-button link class="appbar-icon" @click="toggleMenu">
        <span class="material-icons">menu</span>
      </el-button>
      <div class="brand-mark"><span class="material-icons">send</span></div>
      <div class="app-title">
        <strong>Telegram X</strong>
        <small>Operations Console · v{{ appReleaseVersion }}</small>
      </div>
      <div class="appbar-spacer" />
      <el-button link class="appbar-icon appbar-secondary" title="重启面板" :disabled="restartPanelLoading" @click="restartPanel">
        <span class="material-icons">{{ restartPanelLoading ? 'hourglass_empty' : 'restart_alt' }}</span>
      </el-button>
      <el-button link class="appbar-icon appbar-secondary" title="系统设置" @click="router.push('/settings')">
        <span class="material-icons">settings</span>
      </el-button>
      <el-button link class="appbar-icon" :title="isDark ? '切换到白天模式' : '切换到黑夜模式'" @click="toggleTheme">
        <span class="material-icons">{{ isDark ? 'light_mode' : 'dark_mode' }}</span>
      </el-button>
      <el-dropdown @command="onCommand">
        <span class="user-menu">
          <el-avatar :size="28">{{ auth.me?.username?.[0]?.toUpperCase() || 'A' }}</el-avatar>
          <span v-if="!isMobile" class="user-identity">
            <strong>{{ auth.me?.username || 'admin' }}</strong>
            <small>{{ currentRoleLabel }}</small>
          </span>
          <span class="material-icons user-arrow">keyboard_arrow_down</span>
        </span>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item command="logout">退出登录</el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </el-header>

    <el-container class="shell">
      <el-aside v-if="!isEmbedMode && !isMobile" :width="collapsed ? '76px' : '264px'" class="aside">
      <div :class="['workspace-card', { compact: collapsed }]">
        <span class="workspace-pulse" />
        <div v-if="!collapsed"><strong>运营工作台</strong><small>所有服务运行正常</small></div>
      </div>
      <el-menu
        :collapse="collapsed"
        :default-active="activeIndex"
        :default-openeds="defaultOpeneds"
        background-color="transparent"
        :text-color="menuTextColor"
        :active-text-color="menuActiveTextColor"
        class="menu"
        @select="handleSelect"
      >
        <template v-for="item in menuItems" :key="item.index">
          <el-menu-item v-if="!item.children" :index="item.index">
            <MenuIcon :icon="item.icon" />
            <template #title>{{ item.label }}</template>
          </el-menu-item>
          <el-sub-menu v-else :index="item.index">
            <template #title>
              <MenuIcon :icon="item.icon" />
              <span>{{ item.label }}</span>
            </template>
            <el-menu-item v-for="child in item.children" :key="child.index" :index="child.index">
              <MenuIcon :icon="child.icon" />
              <template #title>{{ child.label }}</template>
            </el-menu-item>
          </el-sub-menu>
        </template>
      </el-menu>
    </el-aside>

    <el-drawer v-if="!isEmbedMode && isMobile" v-model="drawerOpen" direction="ltr" :with-header="false" size="256px">
      <div class="mobile-title">Telegram X</div>
      <el-menu
        :default-active="activeIndex"
        :default-openeds="defaultOpeneds"
        background-color="transparent"
        :text-color="menuTextColor"
        :active-text-color="menuActiveTextColor"
        @select="handleMobileSelect"
      >
        <template v-for="item in menuItems" :key="item.index">
          <el-menu-item v-if="!item.children" :index="item.index">
            <MenuIcon :icon="item.icon" />
            <template #title>{{ item.label }}</template>
          </el-menu-item>
          <el-sub-menu v-else :index="item.index">
            <template #title>
              <MenuIcon :icon="item.icon" />
              <span>{{ item.label }}</span>
            </template>
            <el-menu-item v-for="child in item.children" :key="child.index" :index="child.index">
              <MenuIcon :icon="child.icon" />
              <template #title>{{ child.label }}</template>
            </el-menu-item>
          </el-sub-menu>
        </template>
      </el-menu>
    </el-drawer>

    <el-container>
      <el-main class="main">
        <div v-if="!isEmbedMode" class="page-heading">
          <div>
            <div class="page-breadcrumb">工作台 / {{ pageTitle }}</div>
            <h1>{{ pageTitle }}</h1>
          </div>
          <el-tag v-if="auth.isReadOnly" type="info" effect="plain" round>
            <span class="material-icons role-lock">lock</span>只读模式
          </el-tag>
        </div>
        <router-view />
      </el-main>
    </el-container>
    </el-container>
  </el-container>

</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { panelApi } from '@/api/panel'
import type { ModuleNavItem } from '@/api/types'
import { ElMessage, ElMessageBox } from 'element-plus'
import MenuIcon from '@/components/MenuIcon.vue'
import { APP_RELEASE_VERSION } from '@/config/appVersion'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const appReleaseVersion = APP_RELEASE_VERSION
const collapsed = ref(false)
const drawerOpen = ref(false)
const isMobile = ref(window.innerWidth < 780)
const moduleNavItems = ref<ModuleNavItem[]>([])
const isDark = ref(false)
const restartPanelLoading = ref(false)

const pageTitle = computed(() => (route.meta.title as string) || '')
const activeIndex = computed(() => (route.path === '/dictionaries' ? '/data-dictionaries' : route.path))
const isEmbedMode = computed(() => route.query.embed === '1')
const defaultOpeneds: string[] = []
const menuTextColor = computed(() => (isDark.value ? '#c6d2e5' : '#44516a'))
const menuActiveTextColor = computed(() => '#ffffff')

interface MenuItem {
  index: string
  label: string
  icon: string
  external?: boolean
  roles?: string[]
  children?: MenuItem[]
}

const staticMenuItems: MenuItem[] = [
  { index: '/dashboard', label: '仪表盘', icon: 'dashboard' },
  {
    index: 'accounts-group',
    label: '账号管理',
    icon: 'account_circle',
    children: [
      { index: '/accounts', label: '账号列表', icon: 'people' },
      { index: '/accounts/import', label: '导入账号', icon: 'upload', roles: ['admin', 'operator'] },
      { index: '/accounts/login', label: '手动登录', icon: 'login', roles: ['admin', 'operator'] },
      { index: '/accounts/categories', label: '账号分类', icon: 'category', roles: ['admin', 'operator'] },
    ],
  },
  { index: 'customers-group', label: '客户管理', icon: 'contacts', roles: ['admin', 'operator'], children: [
    { index: '/customers', label: '客户列表', icon: 'people' },
    { index: '/customers/lookup', label: '账号筛选', icon: 'manage_search' },
    { index: '/customers/categories', label: '客户分类', icon: 'category' },
  ] },
  { index: '/proxies', label: '代理管理', icon: 'vpn_lock' },
  {
    index: 'channels-group',
    label: '频道管理',
    icon: 'campaign',
    children: [
      { index: '/channels', label: '频道列表', icon: 'list' },
      { index: '/channels/create', label: '创建频道', icon: 'add', roles: ['admin', 'operator'] },
      { index: '/channels/groups', label: '频道分类', icon: 'folder', roles: ['admin', 'operator'] },
    ],
  },
  {
    index: 'groups-group',
    label: '群组管理',
    icon: 'group',
    children: [
      { index: '/groups', label: '群组列表', icon: 'list' },
      { index: '/groups/create', label: '创建群组', icon: 'add', roles: ['admin', 'operator'] },
      { index: '/groups/categories', label: '群组分类', icon: 'folder', roles: ['admin', 'operator'] },
    ],
  },
  {
    index: 'bots-group',
    label: '机器人管理',
    icon: 'smart_toy',
    children: [
      { index: '/bots', label: '机器人列表', icon: 'smart_toy' },
      { index: '/bots/channels', label: 'Bot 频道', icon: 'list' },
    ],
  },
  { index: '/tasks', label: '任务中心', icon: 'assignment' },
  { index: '/data-dictionaries', label: '数据字典', icon: 'menu_book' },
  { index: '/users', label: '团队与权限', icon: 'manage_accounts', roles: ['admin'] },
  { index: '/modules', label: '模块管理', icon: 'extension', roles: ['admin'] },
  { index: '/apis', label: 'API 管理', icon: 'link', roles: ['admin'] },
  { index: '/device-profiles', label: '设备指纹', icon: 'fingerprint', roles: ['admin'] },
  { index: '/settings', label: '系统设置', icon: 'settings', roles: ['admin'] },
  { index: 'logout', label: '退出登录', icon: 'logout' },
]

const menuItems = computed<MenuItem[]>(() => {
  const currentRole = auth.role
  const allowedNavigation = auth.me?.navigationItems
  const roleVisible = (item: MenuItem) => !item.roles || item.roles.includes(currentRole)
  const navigationVisible = (item: MenuItem) => {
    if (item.index === 'logout' || allowedNavigation == null || allowedNavigation.includes(item.index)) return true
    if (item.index.startsWith('/accounts')) return allowedNavigation.includes('accounts-group')
    if (item.index.startsWith('/customers')) return allowedNavigation.includes('customers-group') || allowedNavigation.includes('/customers')
    if (item.index.startsWith('/channels')) return allowedNavigation.includes('channels-group')
    if (item.index.startsWith('/groups')) return allowedNavigation.includes('groups-group')
    if (item.index.startsWith('/bots')) return allowedNavigation.includes('bots-group')
    return false
  }
  const visible = (item: MenuItem) => roleVisible(item) && navigationVisible(item)
  const items = staticMenuItems
    .filter(visible)
    .map((item) => ({ ...item, children: item.children?.filter(visible) }))
  const mergedModuleItems = new Map<string, ModuleNavItem>()

  for (const item of moduleNavItems.value) {
    const href = normalizeModuleHref(item.href)
    if (!href || href === '/modules') continue
    mergedModuleItems.set(href, item)
  }

  const extensionChildren = Array.from(mergedModuleItems.values())
    .sort((a, b) => (a.group || '').localeCompare(b.group || '', 'zh-Hans-CN') || a.order - b.order || a.title.localeCompare(b.title, 'zh-Hans-CN'))
    .map((item) => ({
      index: resolveModuleRoute(item),
      label: item.title,
      icon: resolveModuleIcon(item),
    }))

  if (extensionChildren.length > 0 && auth.isAdmin) {
    const moduleIndex = items.findIndex((x) => x.index === '/modules')
    items.splice(moduleIndex + 1, 0, {
      index: 'extensions-group',
      label: '扩展模块',
      icon: 'extension',
      children: extensionChildren,
    })
  }

  return items
})

const currentRoleLabel = computed(() => {
  if (auth.role === 'admin') return '管理员'
  if (auth.role === 'operator') return '运营人员'
  return '只读审计'
})

function onResize() {
  isMobile.value = window.innerWidth < 780
}

function toggleMenu() {
  if (isMobile.value) drawerOpen.value = true
  else collapsed.value = !collapsed.value
}

function loadStoredTheme() {
  return localStorage.getItem('telegram-panel-theme') === 'dark'
}

function applyTheme(dark: boolean) {
  isDark.value = dark
  document.documentElement.classList.toggle('dark', dark)
}

function toggleTheme() {
  applyTheme(!isDark.value)
  localStorage.setItem('telegram-panel-theme', isDark.value ? 'dark' : 'light')
}

function handleSelect(index: string) {
  if (index === 'logout') {
    auth.logout()
    return
  }

  if (index.startsWith('direct:')) {
    window.location.href = index.slice('direct:'.length)
    return
  }

  router.push(index)
}

function handleMobileSelect(index: string) {
  drawerOpen.value = false
  handleSelect(index)
}

function onCommand(command: string) {
  if (command === 'logout') auth.logout()
}

onMounted(() => {
  applyTheme(loadStoredTheme())
  window.addEventListener('resize', onResize)
  void loadModuleNav()
})
onUnmounted(() => window.removeEventListener('resize', onResize))

function normalizeModuleHref(href: string) {
  if (!href) return '/modules'
  if (href.startsWith('/ui/')) return href.slice(3) || '/'
  if (href.startsWith('/')) return href
  return `/${href}`
}

function resolveModuleRoute(item: ModuleNavItem) {
  const pageKey = (item.pageKey || '').trim()
  if (item.moduleId && pageKey) {
    return `/ext/${encodeURIComponent(item.moduleId)}/${encodeURIComponent(pageKey)}`
  }

  const href = normalizeModuleHref(item.href)
  const match = href.match(/^\/ext\/([^/?#]+)\/([^/?#]+)/i)
  if (match) {
    try {
      return `/ext/${encodeURIComponent(decodeURIComponent(match[1]))}/${encodeURIComponent(decodeURIComponent(match[2]))}`
    } catch {
      // 损坏的百分号编码不能影响整个主布局，交由原始模块地址处理。
    }
  }

  if (item.uiMode === 'direct') {
    return `direct:${href}`
  }

  return href
}

function resolveModuleIcon(item: ModuleNavItem) {
  return (item.icon || '').trim() || 'extension'
}

async function loadModuleNav() {
  try {
    moduleNavItems.value = await panelApi.moduleNav()
  } catch {
    moduleNavItems.value = []
  }
}

async function restartPanel() {
  if (restartPanelLoading.value) return

  await ElMessageBox.confirm(
    '将请求面板服务重启。Docker、系统服务或桌面版守护进程会自动拉起；如果是直接运行二进制，需要由外部守护负责重新启动。是否继续？',
    '确认重启面板',
    {
      type: 'warning',
      confirmButtonText: '重启',
      cancelButtonText: '取消',
    },
  )

  restartPanelLoading.value = true
  try {
    const result = await panelApi.restartPanel()
    ElMessage.success(result.message || '已提交重启请求')
    window.setTimeout(() => {
      window.location.reload()
    }, 8000)
  } finally {
    window.setTimeout(() => {
      restartPanelLoading.value = false
    }, 10000)
  }
}

</script>
