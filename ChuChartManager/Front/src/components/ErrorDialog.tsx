import { defineComponent, ref } from 'vue'
import { Modal, Button, addToast } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { errorDialogShow, errorMessage, errorDetail } from '@/utils/globalCapture'

const REPO_URL = 'https://github.com/MuNET-OSS/ChuChartManager'

export default defineComponent({
  setup() {
    const { t } = useI18n()
    const showDetail = ref(false)

    const copy = async () => {
      try {
        await navigator.clipboard.writeText(`${errorMessage.value}\n\n${errorDetail.value}`)
        addToast({ message: t('error.copied'), type: 'success' })
      } catch {
        addToast({ message: t('error.copyFailed'), type: 'error' })
      }
    }

    const report = () => {
      const title = `[Bug] ${errorMessage.value}`.slice(0, 120)
      const body = `## 问题描述\n\n\n## 错误信息\n\`\`\`\n${errorDetail.value}\n\`\`\``
      const url = `${REPO_URL}/issues/new?title=${encodeURIComponent(title)}&body=${encodeURIComponent(body)}`
      window.open(url, '_blank')
    }

    return () => (
      <Modal
        width="min(85vw,45em)"
        title={t('error.title')}
        v-model:show={errorDialogShow.value}
      >
        {{
          default: () => (
            <div class="flex flex-col gap-3">
              <div class="c-#d33 break-all">{errorMessage.value}</div>

              <div
                class="text-sm op-60 cursor-pointer flex items-center gap-1"
                onClick={() => showDetail.value = !showDetail.value}
              >
                <div class={showDetail.value ? 'i-mdi-chevron-down' : 'i-mdi-chevron-right'} />
                {t('error.detail')}
              </div>
              {showDetail.value && (
                <pre class="text-xs bg-black/5 rd p-2 of-auto max-h-50vh whitespace-pre-wrap break-all m-0">
                  {errorDetail.value}
                </pre>
              )}
            </div>
          ),
          actions: () => (
            <>
              <Button onClick={copy}>{t('error.copy')}</Button>
              <Button onClick={report}>{t('error.report')}</Button>
            </>
          ),
        }}
      </Modal>
    )
  },
})
