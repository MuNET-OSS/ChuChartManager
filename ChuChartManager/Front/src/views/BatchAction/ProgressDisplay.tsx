import { defineComponent, ref } from 'vue'
import { useI18n } from 'vue-i18n'

export const progressCurrent = ref(0)
export const progressAll = ref(100)
export const currentProcessItem = ref('')

export default defineComponent({
  setup() {
    const { t } = useI18n()
    return () => {
      const pct = progressAll.value > 0
        ? Math.floor((progressCurrent.value / progressAll.value) * 100)
        : 0
      return (
        <div class="flex flex-col gap-3 mt-4">
          <h3 class="text-lg font-bold m-0">{t('batch.executing')}</h3>
          <div class="text-sm">
            {t('batch.currentProgress')}: {progressCurrent.value} / {progressAll.value}
          </div>
          <div class="text-sm op-70 break-all min-h-5">
            {currentProcessItem.value && `${t('batch.currentProcessing')}: ${currentProcessItem.value}`}
          </div>
          <div class="w-full h-2 rounded-full bg-[oklch(0.93_0.01_var(--hue))] overflow-hidden">
            <div
              class="h-full rounded-full bg-[oklch(0.6_0.15_var(--hue))] transition-all duration-300"
              style={{ width: `${pct}%` }}
            />
          </div>
          <div class="text-xs op-40 text-right">{pct}%</div>
        </div>
      )
    }
  },
})
