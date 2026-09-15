import axios from 'axios'
import { ElMessage } from 'element-plus'

export const api = axios.create({
  baseURL: '/api/panel',
  timeout: 30_000,
  withCredentials: true,
})

let lastGatewayMessageAt = 0

/** 从 ASP.NET OperationResult/ProblemDetails/验证错误中提取可读消息。 */
export function extractApiErrorMessage(data: unknown): string {
  if (typeof data === 'string') return data.trim()
  if (!data || typeof data !== 'object') return ''

  const body = data as Record<string, unknown>
  for (const key of ['message', 'detail', 'error']) {
    const value = body[key]
    if (typeof value === 'string' && value.trim()) return value.trim()
  }

  const errors = body.errors
  if (errors && typeof errors === 'object') {
    const messages = Object.values(errors as Record<string, unknown>)
      .flatMap((value) => Array.isArray(value) ? value : [value])
      .filter((value): value is string => typeof value === 'string' && value.trim().length > 0)
      .map((value) => value.trim())
    if (messages.length > 0) return messages.join('；')
  }

  const title = body.title
  if (typeof title === 'string' && title.trim()) return title.trim()

  return ''
}

api.interceptors.response.use(
  (res) => res,
  async (err) => {
    if (err?.response?.status === 401 && window.location.pathname !== '/ui/login') {
      window.location.href = `/ui/login?redirect=${encodeURIComponent(window.location.pathname + window.location.search)}`
      return Promise.reject(err)
    }

    const status = Number(err?.response?.status || 0)
    const config = err?.config as (typeof err.config & { __gatewayRetry?: number }) | undefined
    if ([502, 503, 504].includes(status) && config?.method?.toLowerCase() === 'get') {
      const attempt = config.__gatewayRetry || 0
      if (attempt < 2) {
        config.__gatewayRetry = attempt + 1
        await new Promise((resolve) => window.setTimeout(resolve, attempt === 0 ? 1000 : 3000))
        return api.request(config)
      }
    }

    const message = [502, 503, 504].includes(status)
      ? `服务暂时连接失败（${status}），请稍后点击重新加载`
      : extractApiErrorMessage(err?.response?.data) || err?.message
    const code = err?.response?.data?.code
    // 需要用户确认的风控响应由业务页面弹窗处理，不重复显示全局错误提示。
    if (message && code !== 'ACCOUNT_RISK_CONFIRMATION_REQUIRED') {
      const now = Date.now()
      if (![502, 503, 504].includes(status) || now - lastGatewayMessageAt > 2500) {
        ElMessage.error(message)
        if ([502, 503, 504].includes(status)) lastGatewayMessageAt = now
      }
    }
    return Promise.reject(err)
  },
)
