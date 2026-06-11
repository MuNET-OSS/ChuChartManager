import { computed, ref } from 'vue'
import { useStorage } from '@vueuse/core'
import { locale } from '@/locales'
import { appVersion } from '@/store/refs'

// TODO(部署): 由运维部署 COS bucket 后填入实际地址
// 约定文件：
//   {COS_BASE}/ccm.json                      → { "version": "26.1" }
//   {COS_BASE}/ccm-changelog/{ver}.{loc}.md  → 对应版本/语言的更新日志
// 未部署时 fetch 失败，静默降级为「无更新」
const COS_BASE = ''

export interface AppUpdateInfo {
  version: string
}

export const appUpdateInfo = ref<AppUpdateInfo | null>(null)

export const getCleanVersion = (v: string) => v.split('+')[0]

function compareVersion(a: string, b: string): number {
  const pa = getCleanVersion(a).split('.').map(Number)
  const pb = getCleanVersion(b).split('.').map(Number)
  const len = Math.max(pa.length, pb.length)
  for (let i = 0; i < len; i++) {
    const da = pa[i] || 0
    const db = pb[i] || 0
    if (da !== db) return da - db
  }
  return 0
}

export const hasUpdate = computed(() => {
  const remote = appUpdateInfo.value?.version
  const local = appVersion.value?.version
  if (!remote || !local) return false
  return compareVersion(remote, local) > 0
})

export async function checkAppUpdate() {
  if (!COS_BASE) return
  try {
    const res = await fetch(`${COS_BASE}/ccm.json`, { cache: 'no-cache' })
    if (!res.ok) return
    appUpdateInfo.value = await res.json()
    if (appUpdateInfo.value?.version) eagerFetchChangelog(appUpdateInfo.value.version)
  } catch (e) {
    console.error('Failed to get app update info:', e)
  }
}

export const showChangelogModal = ref(false)
export const changelogContent = ref('')
export const changelogTargetVersion = ref('')
export const changelogAutoPopupDone = ref(false)
export const lastShownChangelogVersion = useStorage('ccm-last-shown-changelog', '')

function getLocaleFallbackChain(): string[] {
  const current = locale.value
  const chain = [current]
  if (current.includes('-')) chain.push(current.split('-')[0])
  if (!chain.includes('en')) chain.push('en')
  return chain
}

async function fetchChangelog(ver: string): Promise<string> {
  if (!COS_BASE) return ''
  const cleanVer = getCleanVersion(ver)
  for (const loc of getLocaleFallbackChain()) {
    try {
      const res = await fetch(`${COS_BASE}/ccm-changelog/${cleanVer}.${loc}.md`, { cache: 'no-cache' })
      if (res.ok) return await res.text()
    } catch {
      // 网络错误，尝试下一个 locale
    }
  }
  return ''
}

const changelogCache = new Map<string, Promise<string>>()
let openChangelogRequestId = 0

function getChangelogCacheKey(ver: string) {
  return `${getCleanVersion(ver)}|${getLocaleFallbackChain().join('>')}`
}

export function eagerFetchChangelog(ver: string) {
  const key = getChangelogCacheKey(ver)
  if (!changelogCache.has(key)) changelogCache.set(key, fetchChangelog(ver))
}

async function getChangelogCached(ver: string): Promise<string> {
  const key = getChangelogCacheKey(ver)
  const cached = changelogCache.get(key)
  if (cached) return cached
  const promise = fetchChangelog(ver)
  changelogCache.set(key, promise)
  return promise
}

export async function openChangelog(ver: string, options?: { showAfterLoaded?: boolean; skipIfEmpty?: boolean }) {
  const requestId = ++openChangelogRequestId
  const cleanVer = getCleanVersion(ver)
  const showAfterLoaded = !!options?.showAfterLoaded
  const skipIfEmpty = !!options?.skipIfEmpty

  changelogTargetVersion.value = cleanVer
  changelogContent.value = ''

  if (!showAfterLoaded) showChangelogModal.value = true

  const content = await getChangelogCached(ver)
  if (requestId !== openChangelogRequestId) return false
  if (skipIfEmpty && !content) {
    showChangelogModal.value = false
    return false
  }

  changelogContent.value = content
  if (showAfterLoaded) showChangelogModal.value = true
  return true
}
