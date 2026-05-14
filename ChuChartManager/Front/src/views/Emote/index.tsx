import { defineComponent, ref, onMounted } from 'vue'
import { Button, addToast } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import {
  getEmoteDataList, launchViewer, getEmoteWebGLDataUrl,
  type EmoteDataItem,
} from '@/api/emote'
import EmotePlayerCanvas from './EmotePlayerCanvas'

function formatFileSize(bytes: number): string {
  return (bytes / 1024 / 1024).toFixed(2) + ' MB'
}

export default defineComponent({
  setup() {
    const { t } = useI18n()

    const loading = ref(false)
    const dataList = ref<EmoteDataItem[]>([])
    const selectedData = ref<EmoteDataItem | null>(null)

    async function loadData() {
      loading.value = true
      try { dataList.value = await getEmoteDataList() }
      finally { loading.value = false }
    }

    async function handlePreview(item: EmoteDataItem) {
      try {
        await launchViewer(item.filePath)
        addToast({ message: t('emote.viewerLaunched'), type: 'success' })
      } catch (e: unknown) {
        const axiosErr = e as { response?: { data?: unknown }; message?: string }
        const msg = axiosErr?.response?.data
        addToast({
          message: typeof msg === 'string' ? msg : (
            (msg as { title?: string })?.title || axiosErr?.message || t('emote.launchFailed')
          ),
          type: 'error',
        })
      }
    }

    onMounted(loadData)

    const renderField = (label: string, value: string | number) => (
      <div>
        <label class="text-xs op-50 mb-1 block">{label}</label>
        <div class="text-sm">{value}</div>
      </div>
    )

    return () => (
      <div class="flex h-full">
        <div class="w-72 flex-shrink-0 border-r border-solid border-[oklch(0.9_0.02_var(--hue))] flex flex-col">
          <div class="flex items-center justify-between p-3 border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
            <h3 class="text-base font-bold m-0">{t('emote.data')}</h3>
          </div>

          <div class="flex-1 of-y-auto">
            {loading.value ? (
              <div class="text-center op-40 py-6">{t('common.loading')}</div>
            ) : dataList.value.length === 0 ? (
              <div class="text-center op-40 py-6">{t('emote.noData')}</div>
            ) : dataList.value.map(d => (
              <div
                key={`${d.id}-${d.assetDir}`}
                class={[
                  'px-3 py-2.5 cursor-pointer border-b border-solid border-[oklch(0.93_0.01_var(--hue))] transition-colors',
                  selectedData.value?.id === d.id && selectedData.value?.assetDir === d.assetDir
                    ? 'bg-[oklch(0.92_0.05_var(--hue))]'
                    : 'hover:bg-[oklch(0.97_0.02_var(--hue))]',
                ]}
                onClick={() => { selectedData.value = d }}
              >
                <div class="text-sm font-medium truncate">{d.fileName}</div>
                <div class="text-xs op-40 mt-0.5 flex items-center gap-2 flex-wrap">
                  <span>ID: {d.id}</span>
                  <span>·</span>
                  <span>{d.assetDir}</span>
                  <span>·</span>
                  <span>{formatFileSize(d.fileSize)}</span>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div class="flex-1 min-w-0 flex flex-col">
          {selectedData.value ? (() => {
            const d = selectedData.value!
            return (
              <>
                <div class="flex items-center gap-3 p-4 border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
                  <h2 class="text-lg font-bold m-0 flex-1 truncate">{d.fileName}</h2>
                  <Button onClick={() => handlePreview(d)}>
                    <span class="i-mdi-play text-3.5 mr-1" />
                    {t('emote.preview')}
                  </Button>
                </div>
                <div class="flex-1 of-y-auto p-4">
                  <EmotePlayerCanvas
                    dataUrl={getEmoteWebGLDataUrl(d.filePath)}
                    width={640}
                    height={480}
                  />
                  <div class="grid grid-cols-2 gap-4 mb-4 mt-4">
                    {renderField(t('emote.id'), d.id)}
                    {renderField(t('emote.fileName'), d.fileName)}
                    {renderField(t('emote.fileSize'), formatFileSize(d.fileSize))}
                  </div>
                  <div class="text-xs op-30 mt-4">{t('emote.assetDir')}: {d.assetDir}</div>
                </div>
              </>
            )
          })() : (
            <div class="flex-1 flex items-center justify-center op-30">
              <div class="text-center">
                <span class="i-mdi-emoticon-outline text-16 block mb-2" />
                <span class="text-sm">{t('emote.data')}</span>
              </div>
            </div>
          )}
        </div>
      </div>
    )
  },
})
