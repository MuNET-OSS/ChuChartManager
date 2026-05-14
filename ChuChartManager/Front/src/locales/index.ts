import { createI18n } from 'vue-i18n'
import zh from './zh.yaml'
import en from './en.yaml'
import ja from './ja.yaml'
import { apiClient } from '@/api'

export const availableLocales = ['zh', 'en', 'ja'] as const
export type Locale = typeof availableLocales[number]

const localeMessages: Record<string, any> = { zh, en, ja }

const localeLabels: Record<Locale, string> = {
  zh: '简体中文',
  en: 'English',
  ja: '日本語',
}

export { localeLabels }

const detectLocale = (): Locale => {
  const lang = navigator.language
  if (lang.startsWith('zh')) return 'zh'
  if (lang.startsWith('ja')) return 'ja'
  return 'en'
}

const i18n = createI18n({
  legacy: false,
  locale: detectLocale(),
  fallbackLocale: 'en',
  messages: localeMessages,
  globalInjection: true,
})

export const setLocale = async (locale: Locale) => {
  i18n.global.locale.value = locale
  document.documentElement.lang = locale

  try {
    await apiClient.post('/api/Config/SetLocale', JSON.stringify(locale), {
      headers: { 'Content-Type': 'application/json' },
    })
  } catch (error) {
    console.error('Failed to save locale to backend:', error)
  }
}

export const loadLocaleFromBackend = async () => {
  try {
    const { data } = await apiClient.get('/api/Config/GetLocale')
    if (availableLocales.includes(data as Locale)) {
      i18n.global.locale.value = data as Locale
      document.documentElement.lang = data
    }
  } catch (error) {
    console.error('Failed to load locale from backend:', error)
  }
}

export const locale = i18n.global.locale

export default i18n
