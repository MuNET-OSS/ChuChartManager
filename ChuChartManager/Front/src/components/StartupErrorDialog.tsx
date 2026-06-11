import { defineComponent, onMounted, ref } from 'vue'
import { Modal } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { getStartupErrors } from '@/api'

export default defineComponent({
  setup() {
    const { t } = useI18n()
    const show = ref(false)
    const errors = ref<string[]>([])

    onMounted(async () => {
      errors.value = await getStartupErrors()
      if (errors.value.length) show.value = true
    })

    return () => (
      <Modal
        width="min(85vw,40em)"
        title={t('startup.errorTitle')}
        v-model:show={show.value}
      >
        <div class="flex flex-col gap-2 max-h-60vh overflow-y-auto">
          <div class="flex flex-col gap-1">
            {errors.value.map(error => (
              <div class="text-0.9em break-all">{error}</div>
            ))}
          </div>
          <div class="op-60 mt-2">{t('startup.fixPrompt')}</div>
        </div>
      </Modal>
    )
  },
})
