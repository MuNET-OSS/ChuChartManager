import '@unocss/reset/tailwind-compat.css'
import 'virtual:uno.css'
import '@fontsource/noto-sans-sc'
import '@fontsource/quicksand'
import { createApp } from 'vue'
import App from './App.vue'
import './global.sass'
import { initThemeDefaults, selectedThemeName, UIThemes } from '@munet/ui'
import i18n from '@/locales'

initThemeDefaults({ hue: 353 })
selectedThemeName.value = UIThemes.DynamicLight

if ((window as any).chrome?.webview) {
  (window as any).chrome.webview.addEventListener('message', (e: any) => {
    ;(globalThis as any).backendUrl = e.data
    import('./api').then(m => {
      m.apiClient.defaults.baseURL = e.data
    })
  })
}

createApp(App)
  .use(i18n)
  .mount('#app')
