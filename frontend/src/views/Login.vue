<template>
  <main class="login-page">
    <section class="login-story">
      <div class="story-grid" />
      <div class="story-orb orb-one" />
      <div class="story-orb orb-two" />
      <div class="story-content">
        <div class="story-brand"><span class="material-icons">send</span><strong>Telegram Panel</strong></div>
        <div class="story-copy">
          <div class="eyebrow">OPERATIONS CONSOLE</div>
          <h1>让每个账号、任务和结果<br><span>清晰可控。</span></h1>
          <p>统一管理账号、代理、群组与自动化任务，为团队提供稳定的日常运营工作台。</p>
        </div>
        <div class="story-status">
          <span class="status-light" />
          <div><strong>服务已就绪</strong><small>安全登录 · 权限隔离 · 操作留痕</small></div>
        </div>
      </div>
    </section>

    <section class="login-form-side">
      <div class="login-panel">
        <div class="mobile-brand"><span class="material-icons">send</span>Telegram Panel</div>
        <div class="login-heading">
          <span>欢迎回来</span>
          <h2>登录运营工作台</h2>
          <p>请输入团队成员账号继续</p>
        </div>
        <el-alert v-if="error" :title="error" type="error" show-icon :closable="false" class="login-alert" />
        <el-form label-position="top" @submit.prevent="submit">
          <el-form-item label="用户名">
            <el-input v-model="username" size="large" placeholder="输入用户名" autocomplete="username">
              <template #prefix><span class="material-icons input-icon">person</span></template>
            </el-input>
          </el-form-item>
          <el-form-item label="密码">
            <el-input v-model="password" size="large" placeholder="输入密码" type="password" show-password autocomplete="current-password">
              <template #prefix><span class="material-icons input-icon">lock</span></template>
            </el-input>
          </el-form-item>
          <el-button type="primary" size="large" :loading="loading" class="login-btn" native-type="submit">
            登录工作台<span class="material-icons">arrow_forward</span>
          </el-button>
        </el-form>
        <div class="login-footnote"><span class="material-icons">verified_user</span>登录身份将根据角色自动分配权限</div>
      </div>
    </section>
  </main>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const username = ref('')
const password = ref('')
const loading = ref(false)
const error = ref('')

async function submit() {
  error.value = ''
  if (!username.value.trim() || !password.value.trim()) {
    error.value = '请输入用户名和密码'
    return
  }
  loading.value = true
  try {
    await auth.login(username.value, password.value)
    if (auth.me?.mustChangePassword) {
      await router.replace({ path: '/admin/password', query: { returnUrl: '/dashboard' } })
      return
    }
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard'
    await router.replace(redirect)
  } catch {
    error.value = '用户名或密码错误，或者账号已停用'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-page { min-height:100vh; display:grid; grid-template-columns:minmax(480px,1.08fr) minmax(420px,.92fr); background:#f8fafc; }
.login-story { position:relative; overflow:hidden; color:#fff; background:linear-gradient(145deg,#07142f 0%,#102a63 52%,#075985 100%); }
.story-grid { position:absolute; inset:0; opacity:.18; background-image:linear-gradient(rgba(255,255,255,.12) 1px,transparent 1px),linear-gradient(90deg,rgba(255,255,255,.12) 1px,transparent 1px); background-size:48px 48px; mask-image:linear-gradient(to bottom right,#000,transparent 78%); }
.story-orb { position:absolute; border-radius:50%; filter:blur(2px); }
.orb-one { width:420px; height:420px; right:-120px; top:-90px; background:radial-gradient(circle at 35% 35%,rgba(56,189,248,.75),rgba(37,99,235,.08) 70%); }
.orb-two { width:280px; height:280px; left:-80px; bottom:-80px; background:radial-gradient(circle at 60% 40%,rgba(45,212,191,.42),transparent 70%); }
.story-content { position:relative; z-index:1; min-height:100%; display:flex; flex-direction:column; justify-content:space-between; padding:44px 56px; }
.story-brand,.mobile-brand { display:flex; align-items:center; gap:10px; font-size:18px; letter-spacing:-.02em; }
.story-brand .material-icons,.mobile-brand .material-icons { display:grid; place-items:center; width:38px; height:38px; border-radius:12px; color:#0f4c81; background:#fff; transform:rotate(-8deg); }
.story-copy { margin:auto 0; max-width:650px; }
.eyebrow { margin-bottom:22px; color:#67e8f9; font-size:12px; font-weight:800; letter-spacing:.2em; }
.story-copy h1 { margin:0; font-size:clamp(44px,5vw,72px); line-height:1.08; letter-spacing:-.055em; }
.story-copy h1 span { color:#67e8f9; }
.story-copy p { max-width:530px; margin:24px 0 0; color:rgba(226,232,240,.7); font-size:16px; line-height:1.8; }
.story-status { display:flex; align-items:center; gap:12px; width:max-content; padding:12px 16px; border:1px solid rgba(255,255,255,.12); border-radius:14px; background:rgba(15,23,42,.35); backdrop-filter:blur(12px); }
.story-status > div { display:grid; gap:2px; }
.story-status small { color:rgba(255,255,255,.55); }
.status-light { width:9px; height:9px; border-radius:50%; background:#34d399; box-shadow:0 0 0 5px rgba(52,211,153,.15),0 0 18px #34d399; }
.login-form-side { display:grid; place-items:center; padding:48px; }
.login-panel { width:min(420px,100%); }
.mobile-brand { display:none; color:#0f172a; font-weight:750; }
.login-heading > span { color:#2563eb; font-size:13px; font-weight:750; }
.login-heading h2 { margin:8px 0; color:#0f172a; font-size:32px; letter-spacing:-.035em; }
.login-heading p { margin:0 0 30px; color:#64748b; }
.login-alert { margin-bottom:18px; }
.login-panel :deep(.el-form-item) { margin-bottom:21px; }
.login-panel :deep(.el-form-item__label) { color:#334155; font-weight:650; }
.login-panel :deep(.el-input__wrapper) { min-height:50px; padding:0 15px; border-radius:12px; border:1px solid #dbe3ef; background:#fff; box-shadow:none; transition:.2s ease; }
.login-panel :deep(.el-input__wrapper.is-focus) { border-color:#2563eb; box-shadow:0 0 0 4px rgba(37,99,235,.1); }
.input-icon { color:#94a3b8; font-size:19px; }
.login-btn { width:100%; height:52px; margin-top:5px; border:0; border-radius:12px; font-weight:750; background:linear-gradient(100deg,#1d4ed8,#0284c7); box-shadow:0 12px 25px rgba(37,99,235,.2); }
.login-btn :deep(span) { display:flex; align-items:center; justify-content:center; gap:8px; }
.login-btn .material-icons { font-size:18px; }
.login-footnote { display:flex; align-items:center; justify-content:center; gap:7px; margin-top:24px; color:#94a3b8; font-size:12px; }
.login-footnote .material-icons { font-size:16px; }
@media (max-width:900px) { .login-page { grid-template-columns:1fr; } .login-story { display:none; } .login-form-side { padding:30px; } .mobile-brand { display:flex; margin-bottom:64px; } }
</style>
