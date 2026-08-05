import { computed, defineComponent, ref } from 'vue'
import { Modal } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { appVersion } from '@/store/refs'
import { appUpdateInfo, hasUpdate, openChangelog } from '@/store/appUpdate'

export default defineComponent({
  setup() {
    const { t } = useI18n()
    const show = ref(false)
    const displayVersion = computed(() => appVersion.value?.version?.split('+')[0])

    return () => appVersion.value && (
      <div
        class="w-15 py-1 flex items-center justify-center rounded-md cursor-pointer transition-all duration-200 bg-avatarMenuButton text-3.5 shrink-0 relative"
        onClick={() => show.value = true}
      >
        v{displayVersion.value}
        {hasUpdate.value && (
          <div class="absolute -top-1 -right-1 w-2.5 h-2.5 rounded-full bg-#f64861 border-2 border-white" />
        )}

        <Modal
          width="min(85vw,60em)"
          title={t('about.title')}
          v-model:show={show.value}
        >
          <div class="flex flex-col gap-3" style={{ containerType: 'inline-size' }}>
            <img src="/logo-wide.png" class="w-[58cqw] mx-auto mt-2 mb-6" />
            <div class="flex justify-center gap-2 text-9 c-gray-4">
              <a
                class="i-mdi-github hover:c-[var(--text-color)] transition-300"
                href="https://github.com/MuNET-OSS/ChuChartManager"
                target="_blank"
                rel="noreferrer"
                aria-label="GitHub"
              />
              <a
                class="i-ri-qq-fill hover:c-[var(--text-color)] transition-300"
                href="https://qm.qq.com/q/JQGnQZVF6w"
                target="_blank"
                rel="noreferrer"
                aria-label="QQ group"
              />
            </div>
            <div>
              <div class="text-sm op-60">{t('about.version')}</div>
              <div>v{appVersion.value.version}</div>
            </div>
            <div>
              <div class="text-sm op-60">{t('about.gameVersion')}</div>
              <div>{appVersion.value.gameVersionStr}</div>
            </div>
            {hasUpdate.value && (
              <div class="flex items-center justify-between gap-2 bg-#f6486118 rd p-2.5">
                <div>
                  <div class="text-sm c-#f64861">{t('about.updateAvailable')}</div>
                  <div class="font-medium">v{appUpdateInfo.value?.version}</div>
                </div>
                <div
                  class="px-3 py-1 rounded-md cursor-pointer bg-avatarMenuButton text-sm"
                  onClick={() => { if (appUpdateInfo.value) openChangelog(appUpdateInfo.value.version) }}
                >
                  {t('about.viewChangelog')}
                </div>
              </div>
            )}
            <div class="op-60 text-center text-xs mt-4">
              © 2026 MuNET Team
              <br />
              Open source under GNU GPL v3
              <br />
              Not affiliated with or endorsed by SEGA.
            </div>
          </div>
        </Modal>
      </div>
    )
  },
})
