import { defineComponent, ref, computed } from 'vue'
import { Button, Modal, TextInput, NumberInput, Select, addToast } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { optionDirs } from '@/store/refs'
import DirSelect from '@/components/DirSelect'
import { openImageFileDialog, createTrophy, createNamePlate, createAvatarAccessory, createMapIcon, createSystemVoice, getResourceList, getLocalImagePreviewUrl } from '@/api/customResource'
import { openAfbFileDialog, openAfbFolderDialog, extractDds } from '@/api/ddsExtractor'
import type { ExtractResult } from '@/api/ddsExtractor'
import CharaCreator from './CharaCreator'

type ModalType = null | 'trophy' | 'namePlate' | 'avatarAccessory' | 'mapIcon' | 'systemVoice' | 'ddsExtractor'

export default defineComponent({
  setup() {
    const { t } = useI18n()
    const activeModal = ref<ModalType>(null)
    const showCharaCreator = ref(false)
    const loading = ref(false)

    const ddsPath = ref('')
    const ddsExtracting = ref(false)
    const ddsResults = ref<ExtractResult[]>([])

    const targetDir = ref('')
    const resourceId = ref(9000)
    const resourceName = ref('')
    const explainText = ref('')
    const rareType = ref(0)
    const imagePath = ref('')
    const iconImagePath = ref('')
    const textureImagePath = ref('')
    const accessoryCategory = ref(1)
    const idConflict = ref(false)
    const idChecking = ref(false)

    const rareOptions = computed<SelectOption[]>(() => [
      { label: t('tools.rareNormal'), value: 0 },
      { label: t('tools.rareBronze'), value: 1 },
      { label: t('tools.rareSilver'), value: 2 },
      { label: t('tools.rareGold'), value: 3 },
      { label: t('tools.rareGoldPlus'), value: 4 },
      { label: t('tools.rarePlatinum'), value: 5 },
      { label: t('tools.rarePlatinumPlus'), value: 6 },
      { label: t('tools.rareRainbow'), value: 7 },
      { label: t('tools.rareRainbowPlus'), value: 8 },
      { label: t('tools.rareStaff'), value: 9 },
      { label: t('tools.rareOngeki'), value: 10 },
      { label: t('tools.rareMaimai'), value: 11 },
      { label: t('tools.rareIrodoriSilver'), value: 13 },
      { label: t('tools.rareIrodoriGold'), value: 14 },
      { label: t('tools.rareIrodoriRainbow'), value: 15 },
      { label: t('tools.rareImage'), value: 20 },
    ])

    const accessoryCategoryOptions = computed<SelectOption[]>(() => [
      { label: t('tools.categoryHead'), value: 1 },
      { label: t('tools.categoryFace'), value: 2 },
      { label: t('tools.categoryBody'), value: 3 },
      { label: t('tools.categoryBack'), value: 4 },
    ])

    const tools = computed(() => [
      { icon: 'i-mdi-package-variant', labelKey: 'ddsExtractor.title', action: () => openModal('ddsExtractor'), experimental: false },
      { icon: 'i-mdi-trophy', labelKey: 'tools.createTrophy', action: () => openModal('trophy'), experimental: true },
      { icon: 'i-mdi-card-account-details', labelKey: 'tools.createNamePlate', action: () => openModal('namePlate'), experimental: true },
      { icon: 'i-mdi-hanger', labelKey: 'tools.createAvatarAccessory', action: () => openModal('avatarAccessory'), experimental: true },
      { icon: 'i-mdi-map-marker', labelKey: 'tools.createMapIcon', action: () => openModal('mapIcon'), experimental: true },
      { icon: 'i-mdi-account', labelKey: 'tools.createChara', action: () => { showCharaCreator.value = true }, experimental: true },
      { icon: 'i-mdi-microphone', labelKey: 'tools.createSystemVoice', action: () => openModal('systemVoice'), experimental: true },
    ])

    function openModal(type: ModalType) {
      activeModal.value = type
      resourceId.value = 9000
      resourceName.value = ''
      explainText.value = ''
      rareType.value = 0
      imagePath.value = ''
      iconImagePath.value = ''
      textureImagePath.value = ''
      accessoryCategory.value = 1
      idConflict.value = false
      const custom = optionDirs.value.filter(d => d.dirName !== 'A000')
      if (custom.length > 0 && !targetDir.value)
        targetDir.value = custom[0].dirName
    }

    function closeModal() {
      activeModal.value = null
    }

    async function selectImage() {
      const path = await openImageFileDialog()
      if (path) imagePath.value = path
    }

    async function selectIconImage() {
      const path = await openImageFileDialog()
      if (path) iconImagePath.value = path
    }

    async function selectTextureImage() {
      const path = await openImageFileDialog()
      if (path) textureImagePath.value = path
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

    async function checkIdConflict() {
      if (!activeModal.value) return
      idChecking.value = true
      try {
        const typeMap: Record<string, string> = {
          trophy: 'trophy', namePlate: 'namePlate', frame: 'frame',
          avatarAccessory: 'avatarAccessory', mapIcon: 'mapIcon',
          systemVoice: 'systemVoice',
        }
        const resType = typeMap[activeModal.value]
        if (!resType) { idConflict.value = false; return }
        const list = await getResourceList(resType as any)
        idConflict.value = list.some(item => item.id === resourceId.value)
      } catch {
        idConflict.value = false
      } finally {
        idChecking.value = false
      }
    }

    async function handleCreate() {
      if (!targetDir.value) {
        addToast({ message: t('tools.targetDir'), type: 'error' })
        return
      }
      loading.value = true
      try {
        switch (activeModal.value) {
          case 'trophy':
            await createTrophy({
              targetDir: targetDir.value,
              id: resourceId.value,
              name: resourceName.value,
              rareType: rareType.value,
              explainText: explainText.value,
              imagePath: rareType.value === 20 ? imagePath.value : undefined,
            })
            break
          case 'namePlate':
            await createNamePlate({
              targetDir: targetDir.value,
              id: resourceId.value,
              name: resourceName.value,
              explainText: explainText.value,
              imagePath: imagePath.value,
            })
            break
          case 'avatarAccessory':
            await createAvatarAccessory({
              targetDir: targetDir.value,
              id: resourceId.value,
              name: resourceName.value,
              explainText: explainText.value,
              category: accessoryCategory.value,
              iconImagePath: iconImagePath.value,
              textureImagePath: textureImagePath.value,
            })
            break
          case 'mapIcon':
            await createMapIcon({
              targetDir: targetDir.value,
              id: resourceId.value,
              name: resourceName.value,
              explainText: explainText.value,
              imagePath: imagePath.value,
            })
            break
          case 'systemVoice':
            await createSystemVoice({
              targetDir: targetDir.value,
              id: resourceId.value,
              name: resourceName.value,
              explainText: explainText.value,
              imagePath: imagePath.value,
            })
            break
        }
        addToast({ message: t('tools.createSuccess'), type: 'success' })
        closeModal()
      } catch (e: any) {
        const msg = e?.response?.data || e?.message || t('tools.createFailed')
        addToast({ message: String(msg), type: 'error' })
      } finally {
        loading.value = false
      }
    }

    const needsImage = computed(() =>
      activeModal.value === 'namePlate' || activeModal.value === 'mapIcon' || activeModal.value === 'systemVoice'
    )

    const modalTitle = computed(() => {
      switch (activeModal.value) {
        case 'ddsExtractor': return t('ddsExtractor.title')
        case 'trophy': return t('tools.createTrophy')
        case 'namePlate': return t('tools.createNamePlate')
        case 'avatarAccessory': return t('tools.createAvatarAccessory')
        case 'mapIcon': return t('tools.createMapIcon')
        case 'systemVoice': return t('tools.createSystemVoice')
        default: return ''
      }
    })

    function renderImageSelector(path: { value: string }, onSelect: () => void, label: string) {
      return (
        <div>
          <label class="block text-sm op-60 mb-1">{label}</label>
          <div class="flex items-center gap-2">
            <Button onClick={onSelect}>{t('tools.selectImage')}</Button>
            <span class="text-sm op-50 truncate">
              {path.value ? path.value.split(/[\\/]/).pop() : t('tools.noImageSelected')}
            </span>
          </div>
          {path.value && (
            <img
              src={getLocalImagePreviewUrl(path.value)}
              class="mt-2 max-h-32 rounded border border-solid border-[oklch(0.85_0.02_var(--hue))] object-contain"
              onError={(e: any) => { e.target.style.display = 'none' }}
            />
          )}
        </div>
      )
    }

    return () => {
      if (showCharaCreator.value) {
        return <CharaCreator onBack={() => { showCharaCreator.value = false }} />
      }

      return (
        <div class="flex flex-col h-full p-6 of-y-auto">
          <h2 class="text-2xl font-bold mb-6">{t('tools.title')}</h2>

          <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4 mb-6">
            {tools.value.filter(item => !item.experimental).map(tool => (
              <div
                key={tool.labelKey}
                class="flex flex-col items-center justify-center gap-3 p-6 rounded-xl cursor-pointer transition-all duration-200 border border-solid border-[oklch(0.85_0.02_var(--hue))] bg-[oklch(0.98_0.005_var(--hue))] hover:bg-[oklch(0.95_0.02_var(--hue))] hover:border-[oklch(0.7_0.1_var(--hue))]"
                onClick={tool.action}
              >
                <span class={[tool.icon, 'text-8 text-[oklch(0.55_0.15_var(--hue))]']} />
                <span class="text-sm text-center font-medium">{t(tool.labelKey)}</span>
              </div>
            ))}
          </div>

          <div class="flex items-center gap-2 mb-4 p-3 rounded-lg bg-[oklch(0.95_0.05_90)] border border-solid border-[oklch(0.8_0.1_90)]">
            <span class="i-mdi-flask text-5 text-[oklch(0.5_0.15_90)]" />
            <span class="text-sm text-[oklch(0.4_0.1_90)]">{t('tools.experimentalWarning')}</span>
          </div>
          <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
            {tools.value.filter(item => item.experimental).map(tool => (
              <div
                key={tool.labelKey}
                class="flex flex-col items-center justify-center gap-3 p-6 rounded-xl cursor-pointer transition-all duration-200 border border-solid border-[oklch(0.85_0.02_var(--hue))] bg-[oklch(0.98_0.005_var(--hue))] hover:bg-[oklch(0.95_0.02_var(--hue))] hover:border-[oklch(0.7_0.1_var(--hue))]"
                onClick={tool.action}
              >
                <span class={[tool.icon, 'text-8 text-[oklch(0.55_0.15_var(--hue))]']} />
                <span class="text-sm text-center font-medium">{t(tool.labelKey)}</span>
              </div>
            ))}
          </div>

          <Modal
            show={activeModal.value !== null}
            title={modalTitle.value}
            width="min(50vw, 36em)"
            onClose={closeModal}
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
                  <div class="p-3 rounded bg-[oklch(0.96_0.005_var(--hue))] text-sm max-h-40 of-y-auto">
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
            ) : (
              <div class="flex flex-col gap-3 p-2">
                <div>
                  <label class="block text-sm op-60 mb-1">{t('tools.targetDir')}</label>
                  <DirSelect v-model:value={targetDir.value} />
                </div>
                <div>
                  <label class="block text-sm op-60 mb-1">{t('tools.resourceId')}</label>
                  <div class="flex items-center gap-2">
                    <NumberInput v-model:value={resourceId.value} min={1} max={99999999} class="flex-1" />
                    <Button onClick={checkIdConflict} ing={idChecking.value}>{t('tools.checkConflict')}</Button>
                  </div>
                  {idConflict.value && (
                    <span class="text-xs text-red-500 mt-1">{t('tools.idConflictWarning')}</span>
                  )}
                </div>
                <div>
                  <label class="block text-sm op-60 mb-1">{t('tools.resourceName')}</label>
                  <TextInput v-model:value={resourceName.value} />
                </div>

                {activeModal.value === 'trophy' && (
                  <div>
                    <label class="block text-sm op-60 mb-1">{t('tools.rareType')}</label>
                    <Select options={rareOptions.value} v-model:value={rareType.value} />
                  </div>
                )}

                {activeModal.value === 'trophy' && rareType.value === 20 && renderImageSelector(imagePath, selectImage, t('tools.trophyImage'))}

                <div>
                  <label class="block text-sm op-60 mb-1">{t('tools.explainText')}</label>
                  <TextInput v-model:value={explainText.value} />
                </div>

                {activeModal.value === 'avatarAccessory' && (
                  <div>
                    <label class="block text-sm op-60 mb-1">{t('tools.accessoryCategory')}</label>
                    <Select options={accessoryCategoryOptions.value} v-model:value={accessoryCategory.value} />
                  </div>
                )}

                {needsImage.value && renderImageSelector(imagePath, selectImage, t('tools.selectImage'))}

                {activeModal.value === 'avatarAccessory' && (
                  <>
                    {renderImageSelector(iconImagePath, selectIconImage, t('tools.iconImage'))}
                    {renderImageSelector(textureImagePath, selectTextureImage, t('tools.textureImage'))}
                  </>
                )}

                <div class="flex justify-end gap-2 mt-2">
                  <Button onClick={closeModal}>{t('common.cancel')}</Button>
                  <Button onClick={handleCreate} ing={loading.value}>{t('common.create')}</Button>
                </div>
              </div>
            )}
          </Modal>
        </div>
      )
    }
  },
})
