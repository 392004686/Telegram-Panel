import { defineStore } from 'pinia'
import { panelApi } from '@/api/panel'
import type { AuthMe } from '@/api/types'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    me: null as AuthMe | null,
    loading: false,
  }),
  getters: {
    role: (state) => state.me?.role || 'auditor',
    isAdmin: (state) => state.me?.role === 'admin' || state.me?.authEnabled === false,
    canOperate: (state) => state.me?.permissions?.includes('operate') || state.me?.authEnabled === false,
    isReadOnly: (state) => state.me?.authEnabled !== false && !state.me?.permissions?.includes('operate'),
  },
  actions: {
    async fetchMe() {
      if (this.loading) return this.me
      this.loading = true
      try {
        this.me = await panelApi.me()
        return this.me
      } finally {
        this.loading = false
      }
    },
    async login(username: string, password: string) {
      this.me = await panelApi.login(username, password)
    },
    async logout() {
      await panelApi.logout()
      this.me = null
      window.location.href = '/ui/login'
    },
  },
})
