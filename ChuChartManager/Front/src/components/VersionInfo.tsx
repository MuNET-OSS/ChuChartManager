import { computed, defineComponent, ref } from 'vue'
import { Modal } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { appVersion } from '@/store/refs'

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

        <Modal
          width="min(85vw,30em)"
          title={t('about.title')}
          v-model:show={show.value}
        >
          <div class="flex flex-col gap-3">
            <div>
              <div class="text-sm op-60">{t('about.version')}</div>
              <div>v{appVersion.value.version}</div>
            </div>
            <div>
              <div class="text-sm op-60">{t('about.gameVersion')}</div>
              <div>{appVersion.value.gameVersionStr}</div>
            </div>
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