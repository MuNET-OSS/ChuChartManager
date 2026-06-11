import { ref } from 'vue'

export const errorDialogShow = ref(false)
export const errorMessage = ref('')
export const errorDetail = ref('')

function extract(error: any): { message: string; detail: string } {
  const resp = error?.response
  if (resp) {
    const data = resp.data
    let msg = ''
    if (typeof data === 'string') msg = data
    else if (data?.title) msg = data.title
    else if (data?.error) msg = data.error
    else if (data?.detail) msg = data.detail

    const method = error.config?.method ? String(error.config.method).toUpperCase() : ''
    const detail = [
      `${method} ${error.config?.url ?? ''}`.trim(),
      `HTTP ${resp.status}`,
      typeof data === 'object' ? JSON.stringify(data, null, 2) : String(data ?? ''),
    ].filter(Boolean).join('\n')
    return { message: msg || `HTTP ${resp.status}`, detail }
  }

  if (error instanceof Error) {
    return { message: error.message, detail: error.stack ?? error.message }
  }

  return { message: String(error), detail: String(error) }
}

export function globalCapture(error: any, context?: string) {
  console.error('[globalCapture]', context ?? '', error)
  const { message, detail } = extract(error)
  errorMessage.value = context ? `${context}: ${message}` : message
  errorDetail.value = detail
  errorDialogShow.value = true
}
