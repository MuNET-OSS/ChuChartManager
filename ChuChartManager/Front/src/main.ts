import '@unocss/reset/tailwind-compat.css'
import 'virtual:uno.css'
import '@fontsource/noto-sans-sc/index.css'
import '@fontsource/quicksand/index.css'
import { createApp } from 'vue'
import App from './App.vue'
import './global.sass'
import { initThemeDefaults, selectedThemeName, UIThemes } from '@munet/ui'
import i18n from '@/locales'
import { globalCapture } from '@/utils/globalCapture'

initThemeDefaults({ hue: 353 })
selectedThemeName.value = UIThemes.DynamicLight

window.addEventListener('unhandledrejection', e => globalCapture(e.reason, 'Unhandled rejection'))

if ((window as any).chrome?.webview) {
  (window as any).chrome.webview.addEventListener('message', (e: any) => {
    ;(globalThis as any).backendUrl = e.data
    import('./api').then(m => {
      m.apiClient.defaults.baseURL = e.data
    })
  })
}

const app = createApp(App)
app.config.errorHandler = err => globalCapture(err, 'Vue error')
app
  .use(i18n)
  .mount('#app')
