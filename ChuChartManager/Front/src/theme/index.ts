import { computed, watch } from 'vue'
import { UIThemes, isLightTheme, registerAppTheme, selectedThemeHue } from '@munet/ui'
import darkStyles from './dark.module.sass'

// 深色主题缺 listItem（浅色由 dynamicLight 提供），用业务主题层补上
registerAppTheme(UIThemes.Dark, { root: '', styles: darkStyles })

/**
 * 当前明暗模式（跟随 MuNET-UI 主题：DynamicLight = light，Dark = dark）
 */
export const themeMode = computed<'light' | 'dark'>(() => (isLightTheme.value ? 'light' : 'dark'))

/** 是否深色模式（给需要 JS 判断的地方用，例如 naive-ui / wavesurfer） */
export const isDark = computed(() => !isLightTheme.value)

/**
 * 把明暗模式同步到 <html data-ccm-theme>，驱动 tokens.sass 的令牌切换，
 * 并通知 WinForms 端同步原生标题栏配色。
 * 在 main.ts 里调用一次
 */
export function initThemeSync() {
  watch(
    themeMode,
    mode => {
      document.documentElement.dataset.ccmTheme = mode
      scheduleWindowChromeSync()
    },
    { immediate: true },
  )

  // hue 变了令牌的实际色值也变，标题栏得跟着重新染
  watch(selectedThemeHue, scheduleWindowChromeSync)
}

let syncTimer: ReturnType<typeof setTimeout> | undefined

function scheduleWindowChromeSync() {
  clearTimeout(syncTimer)
  // 等样式重算完再读令牌，顺便合并 hue 滑块的连续触发
  syncTimer = setTimeout(syncWindowChrome, 60)
}

/**
 * 令牌是 oklch，WinForms 那边只认 #RRGGBB，
 * 借 1×1 canvas 让 Chrome 做一次 sRGB 换算
 */
function tokenHex(name: string, fallback: string, over?: string) {
  try {
    const raw = getComputedStyle(document.body).getPropertyValue(name).trim()
    if (!/^(#|rgb|hsl|oklch|oklab|color\()/.test(raw)) return fallback

    const ctx = document.createElement('canvas').getContext('2d')
    if (!ctx) return fallback

    // 令牌可能带 alpha（--text-color 就是 #ffffffde），先铺底色再叠加，取不透明结果
    if (over) {
      ctx.fillStyle = over
      ctx.fillRect(0, 0, 1, 1)
    }
    ctx.fillStyle = raw
    ctx.fillRect(0, 0, 1, 1)

    const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data
    return `#${[r, g, b].map(v => v.toString(16).padStart(2, '0')).join('')}`
  } catch {
    return fallback
  }
}

/** 把原生窗口（标题栏 + 窗口底色）染成和界面一致的颜色 */
function syncWindowChrome() {
  const host = (window as any).chrome?.webview
  if (!host?.postMessage) return

  const dark = isDark.value
  const bg = tokenHex('--ccm-bg', dark ? '#1c1a1b' : '#fdfbfb')

  host.postMessage({
    type: 'setTheme',
    dark,
    bg,
    fg: tokenHex('--ccm-text', dark ? '#ffffff' : '#1a1a1a', bg),
    line: tokenHex('--ccm-line', dark ? '#3a3638' : '#e5e7eb'),
  })
}
