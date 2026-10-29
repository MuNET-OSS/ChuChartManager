import { defineComponent, ref, computed } from 'vue'
import { Button, Modal, Select, addToast } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { openImageFileDialog, getLocalImagePreviewUrl } from '@/api/customResource'
import { openAfbFileDialog, openAfbFolderDialog, extractDds } from '@/api/ddsExtractor'
import type { ExtractResult } from '@/api/ddsExtractor'
import { convertImgToDds } from '@/api/imgToDds'

type ModalType = null | 'ddsExtractor' | 'imgToDds'

export default defineComponent({
  setup() {
    const { t } = useI18n()
    const activeModal = ref<ModalType>(null)

    const ddsPath = ref('')
    const ddsExtracting = ref(false)
    const ddsResults = ref<ExtractResult[]>([])

    // 图片转 DDS
    const imgPath = ref('')
    const converting = ref(false)
    const imgFormat = ref<'bc1' | 'bc3' | 'bc7'>('bc1')
    const imgWidth = ref(0)
    const imgHeight = ref(0)
    const generateMipMaps = ref(false)

    const tools = [
      { icon: 'i-mdi-package-variant', labelKey: 'ddsExtractor.title', action: () => openModal('ddsExtractor') },
      { icon: 'i-mdi-image-multiple', labelKey: 'imgToDds.title', action: () => openModal('imgToDds') },
    ]

    const formatOptions = computed<SelectOption[]>(() => [
      { label: t('imgToDds.bc1'), value: 'bc1' },
      { label: t('imgToDds.bc3'), value: 'bc3' },
      { label: t('imgToDds.bc7'), value: 'bc7' },
    ])

    function openModal(type: ModalType) {
      activeModal.value = type
      imgPath.value = ''
    }

    function closeModal() {
      activeModal.value = null
    }

    async function selectImgToDdsImage() {
      const path = await openImageFileDialog()
      if (path) imgPath.value = path
    }

    async function selectDdsFile() {
      const path = await openAfbFileDialog()
      if (path) ddsPath.value = path
    }

    async function selectDdsFolder() {
      const path = await openAfbFolderDialog()
      if (path) ddsPath.value = path
    }

    async function handleExtractDds() {
      if (!ddsPath.value) {
        addToast({ message: t('ddsExtractor.noPath'), type: 'error' })
        return
      }
      ddsExtracting.value = true
      ddsResults.value = []
      try {
        const results = await extractDds(ddsPath.value)
        ddsResults.value = results
        const total = results.reduce((s, r) => s + r.ddsCount, 0)
        addToast({ message: t('ddsExtractor.done', { count: total, files: results.length }), type: 'success' })
      } catch (e: any) {
        const msg = e?.response?.data || e?.message || t('ddsExtractor.failed')
        addToast({ message: String(msg), type: 'error' })
      } finally {
        ddsExtracting.value = false
      }
    }

    async function handleConvert() {
      if (!imgPath.value) {
        addToast({ message: t('imgToDds.noImage'), type: 'error' })
        return
      }
      converting.value = true
      try {
        await convertImgToDds({
          sourcePath: imgPath.value,
          format: imgFormat.value,
          width: imgWidth.value || undefined,
          height: imgHeight.value || undefined,
          generateMipMaps: generateMipMaps.value,
        })
        addToast({ message: t('imgToDds.success'), type: 'success' })
        closeModal()
      } catch (e: any) {
        const msg = e?.response?.data || e?.message || t('imgToDds.failed')
        addToast({ message: String(msg), type: 'error' })
      } finally {
        converting.value = false
      }
    }

    const modalTitle = computed(() => {
      switch (activeModal.value) {
        case 'ddsExtractor': return t('ddsExtractor.title')
        case 'imgToDds': return t('imgToDds.title')
        default: return ''
      }
    })

    function renderImageSelector(path: { value: string }, onSelect: () => void, label: string) {
      return (
        <div>
          <label class="block text-sm op-60 mb-1">{label}</label>
          <div class="flex items-center gap-2">
            <Button onClick={onSelect}>{t('imgToDds.selectImage')}</Button>
            <span class="text-sm op-50 truncate">
              {path.value ? path.value.split(/[\\/]/).pop() : t('imgToDds.noImage')}
            </span>
          </div>
          {path.value && (
            <img
              src={getLocalImagePreviewUrl(path.value)}
              class="mt-2 max-h-32 rounded border border-solid border-ccm-line-strong object-contain"
              onError={(e: any) => { e.target.style.display = 'none' }}
            />
          )}
        </div>
      )
    }

    return () => (
      <div class="flex flex-col h-full p-6 of-y-auto">
        <h2 class="text-2xl font-bold mb-6">{t('tools.title')}</h2>

        <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4 mb-6">
          {tools.map(tool => (
            <div
              key={tool.labelKey}
              class="flex flex-col items-center justify-center gap-3 p-6 rounded-xl cursor-pointer transition-all duration-200 border border-solid border-ccm-line-strong bg-ccm-surface hover:bg-ccm-hover hover:border-ccm-accent"
              onClick={tool.action}
            >
              <span class={[tool.icon, 'text-8 text-ccm-accent']} />
              <span class="text-sm text-center font-medium">{t(tool.labelKey)}</span>
            </div>
          ))}
        </div>

        <Modal
          show={activeModal.value !== null}
          title={modalTitle.value}
          width="min(50vw, 36em)"
          onUpdateShow={(v) => { if (!v) closeModal() }}
        >
          {activeModal.value === 'ddsExtractor' ? (
            <div class="flex flex-col gap-3 p-2">
              <p class="text-sm op-50">{t('ddsExtractor.desc')}</p>
              <div class="flex items-center gap-2">
                <Button onClick={selectDdsFile}>{t('ddsExtractor.selectFile')}</Button>
                <Button onClick={selectDdsFolder}>{t('ddsExtractor.selectFolder')}</Button>
              </div>
              <span class="text-sm op-50 truncate">
                {ddsPath.value || t('ddsExtractor.noPathHint')}
              </span>
              {ddsResults.value.length > 0 && (
                <div class="p-3 rounded bg-ccm-surface-2 text-sm max-h-40 of-y-auto">
                  {ddsResults.value.map((r, i) => (
                    <div key={i} class="mb-1">
                      <span class="font-medium">{r.sourceFile.split(/[\\/]/).pop()}</span>
                      <span class="op-50"> → {r.ddsCount} DDS → {r.outputDir}</span>
                    </div>
                  ))}
                </div>
              )}
              <div class="flex justify-end gap-2 mt-2">
                <Button onClick={closeModal}>{t('common.cancel')}</Button>
                <Button onClick={handleExtractDds} ing={ddsExtracting.value}>{t('ddsExtractor.extract')}</Button>
              </div>
            </div>
          ) : activeModal.value === 'imgToDds' ? (
            <div class="flex flex-col gap-3 p-2">
              <p class="text-sm op-50">{t('imgToDds.desc')}</p>
              {renderImageSelector(imgPath, selectImgToDdsImage, t('imgToDds.selectImage'))}
              <div>
                <label class="block text-sm op-60 mb-1">{t('imgToDds.format')}</label>
                <Select options={formatOptions.value} v-model:value={imgFormat.value} />
              </div>
              <div class="flex gap-3">
                <div class="flex-1">
                  <label class="block text-sm op-60 mb-1">{t('imgToDds.width')}<span class="text-xs op-50 ml-1">({t('imgToDds.auto')})</span></label>
                </div>
                <div class="flex-1">
                  <label class="block text-sm op-60 mb-1">{t('imgToDds.height')}<span class="text-xs op-50 ml-1">({t('imgToDds.auto')})</span></label>
                </div>
              </div>
              <label class="flex items-center gap-2 text-sm cursor-pointer">
                <input type="checkbox" v-model={generateMipMaps.value} class="checkbox checkbox-primary" />
                <span>{t('imgToDds.generateMipMaps')}</span>
              </label>
              <div class="flex justify-end gap-2 mt-2">
                <Button onClick={closeModal}>{t('common.cancel')}</Button>
                <Button onClick={handleConvert} ing={converting.value}>{t('imgToDds.convert')}</Button>
              </div>
            </div>
          ) : null}
        </Modal>
      </div>
    )
  },
})
