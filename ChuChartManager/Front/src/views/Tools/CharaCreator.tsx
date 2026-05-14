import { defineComponent, ref, computed, Transition } from 'vue'
import { Button, TextInput, NumberInput, addToast } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { optionDirs } from '@/store/refs'
import DirSelect from '@/components/DirSelect'
import { openImageFileDialog, createChara, addCharaVariant, getLocalImagePreviewUrl } from '@/api/customResource'

interface VariantData {
  slot: number
  rank: number
  name: string
  imagePath: string
  imagePathMid: string
  imagePathSmall: string
}

export default defineComponent({
  emits: ['back'],
  setup(_, { emit }) {
    const { t } = useI18n()
    const loading = ref(false)
    const entered = ref(false)
    requestAnimationFrame(() => { entered.value = true })

    const targetDir = ref('')
    const resourceId = ref(9000)
    const resourceName = ref('')
    const charaWorks = ref('CHUNITHM')
    const charaIllustrator = ref('')
    const imagePath = ref('')
    const imagePathMid = ref('')
    const imagePathSmall = ref('')
    const variants = ref<VariantData[]>([])
    const activeTab = ref(0)

    const custom = optionDirs.value.filter(d => d.dirName !== 'A000')
    if (custom.length > 0) targetDir.value = custom[0].dirName

    function addVariant() {
      if (variants.value.length >= 9) return
      const nextSlot = variants.value.length + 1
      variants.value.push({ slot: nextSlot, rank: 1, name: '', imagePath: '', imagePathMid: '', imagePathSmall: '' })
      activeTab.value = nextSlot
    }

    function removeVariant(idx: number) {
      variants.value.splice(idx, 1)
      variants.value.forEach((v, i) => { v.slot = i + 1 })
      if (activeTab.value > variants.value.length) {
        activeTab.value = Math.max(0, variants.value.length)
      }
    }

    async function selectForCurrent(key: 'full' | 'mid' | 'small') {
      const path = await openImageFileDialog()
      if (!path) return
      if (activeTab.value === 0) {
        if (key === 'full') imagePath.value = path
        else if (key === 'mid') imagePathMid.value = path
        else imagePathSmall.value = path
      } else {
        const v = variants.value[activeTab.value - 1]
        if (!v) return
        if (key === 'full') v.imagePath = path
        else if (key === 'mid') v.imagePathMid = path
        else v.imagePathSmall = path
      }
    }

    const currentImages = computed(() => {
      if (activeTab.value === 0) {
        return { full: imagePath.value, mid: imagePathMid.value, small: imagePathSmall.value }
      }
      const v = variants.value[activeTab.value - 1]
      if (!v) return { full: '', mid: '', small: '' }
      return { full: v.imagePath, mid: v.imagePathMid, small: v.imagePathSmall }
    })

    async function handleCreate() {
      if (!targetDir.value) {
        addToast({ message: t('tools.targetDir'), type: 'error' })
        return
      }
      if (!imagePath.value || !imagePathMid.value || !imagePathSmall.value) {
        addToast({ message: t('tools.charaImagesRequired'), type: 'error' })
        return
      }
      loading.value = true
      try {
        await createChara({
          targetDir: targetDir.value,
          id: resourceId.value,
          name: resourceName.value,
          works: charaWorks.value,
          illustrator: charaIllustrator.value,
          imagePath: imagePath.value,
          imagePathMid: imagePathMid.value,
          imagePathSmall: imagePathSmall.value,
        })
        for (const v of variants.value) {
          if (v.imagePath && v.imagePathMid && v.imagePathSmall) {
            await addCharaVariant({
              targetDir: targetDir.value,
              baseId: resourceId.value,
              variant: v.slot,
              name: v.name || resourceName.value,
              imagePath: v.imagePath,
              imagePathMid: v.imagePathMid,
              imagePathSmall: v.imagePathSmall,
              rank: v.rank,
            })
          }
        }
        addToast({ message: t('tools.createSuccess'), type: 'success' })
        emit('back')
      } catch (e: any) {
        const msg = e?.response?.data || e?.message || t('tools.createFailed')
        addToast({ message: String(msg), type: 'error' })
      } finally {
        loading.value = false
      }
    }

    function renderPreviewCard(src: string, label: string, size: number, selectKey: 'full' | 'mid' | 'small') {
      return (
        <div class="flex flex-col items-center gap-2">
          <div
            class="rounded-lg border-2 border-dashed border-[oklch(0.8_0.05_var(--hue))] flex items-center justify-center cursor-pointer hover:border-[oklch(0.6_0.1_var(--hue))] transition-all of-hidden bg-[oklch(0.97_0.005_var(--hue))]"
            style={{ width: `${size}px`, height: `${size}px` }}
            onClick={() => selectForCurrent(selectKey)}
          >
            {src
              ? <img src={getLocalImagePreviewUrl(src)} class="w-full h-full object-cover" />
              : <span class="i-mdi-plus text-8 op-30" />
            }
          </div>
          <span class="text-xs op-60 text-center">{label}</span>
        </div>
      )
    }

    function renderTabBar() {
      const tabs = [
        { key: 0, label: t('tools.charaBase') },
        ...variants.value.map((v, i) => ({ key: i + 1, label: `${t('tools.variantSlot')} ${v.slot}` })),
      ]
      return (
        <div class="flex items-center gap-1 border-b border-solid border-[oklch(0.85_0.02_var(--hue))]">
          {tabs.map(tab => (
            <div
              key={tab.key}
              class={[
                'px-4 py-2 text-sm cursor-pointer transition-all border-b-2 border-solid',
                activeTab.value === tab.key
                  ? 'border-[oklch(0.55_0.15_var(--hue))] text-[oklch(0.4_0.1_var(--hue))] font-medium'
                  : 'border-transparent op-50 hover:op-80',
              ]}
              onClick={() => { activeTab.value = tab.key }}
            >
              {tab.label}
            </div>
          ))}
          <div
            class="px-3 py-2 text-sm cursor-pointer op-50 hover:op-100 transition-opacity border-b-2 border-solid border-transparent"
            onClick={addVariant}
            title={t('tools.addVariant')}
          >
            +
          </div>
        </div>
      )
    }

    function renderImageRow() {
      const imgs = currentImages.value
      return (
        <div class="flex items-end gap-4 py-4">
          {renderPreviewCard(imgs.full, t('tools.charaFullImage'), 140, 'full')}
          {renderPreviewCard(imgs.mid, t('tools.charaMidImage'), 100, 'mid')}
          {renderPreviewCard(imgs.small, t('tools.charaSmallImage'), 64, 'small')}
        </div>
      )
    }

    function renderTabContent() {
      if (activeTab.value === 0) {
        return (
          <div key={0}>
            {renderImageRow()}
          </div>
        )
      }
      const v = variants.value[activeTab.value - 1]
      if (!v) return null
      return (
        <div key={activeTab.value}>
          <div class="grid grid-cols-2 gap-4 pt-3">
            <div>
              <label class="block text-sm op-60 mb-1">{t('tools.resourceName')}</label>
              <TextInput v-model:value={v.name} placeholder={resourceName.value} />
            </div>
            <div>
              <label class="block text-sm op-60 mb-1">{t('tools.variantRank')}</label>
              <NumberInput v-model:value={v.rank} min={1} max={99} />
            </div>
          </div>
          {renderImageRow()}
          <div class="flex justify-end">
            <Button onClick={() => removeVariant(activeTab.value - 1)}>
              {t('tools.removeVariant')}
            </Button>
          </div>
        </div>
      )
    }

    return () => (
      <div
        class="flex flex-col h-full p-6 of-y-auto transition-all duration-300"
        style={{ opacity: entered.value ? 1 : 0, transform: entered.value ? 'translateY(0)' : 'translateY(12px)' }}
      >
        <div class="flex items-center gap-3 mb-6">
          <Button onClick={() => emit('back')}>
            <span class="i-mdi-arrow-left text-5" />
          </Button>
          <h2 class="text-2xl font-bold">{t('tools.createChara')}</h2>
        </div>

        <div class="flex flex-col gap-4">
          <div>
            <label class="block text-sm op-60 mb-1">{t('tools.targetDir')}</label>
            <DirSelect v-model:value={targetDir.value} />
          </div>
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="block text-sm op-60 mb-1">{t('tools.resourceId')}</label>
              <NumberInput v-model:value={resourceId.value} min={1} max={99999999} />
            </div>
            <div>
              <label class="block text-sm op-60 mb-1">{t('tools.resourceName')}</label>
              <TextInput v-model:value={resourceName.value} />
            </div>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="block text-sm op-60 mb-1">{t('tools.works')}</label>
              <TextInput v-model:value={charaWorks.value} />
            </div>
            <div>
              <label class="block text-sm op-60 mb-1">{t('tools.illustrator')}</label>
              <TextInput v-model:value={charaIllustrator.value} />
            </div>
          </div>

          <div class="mt-2">
            {renderTabBar()}
            <Transition name="fade" mode="out-in">
              {renderTabContent()}
            </Transition>
          </div>

          <div class="flex justify-end mt-2">
            <Button onClick={handleCreate} ing={loading.value}>{t('common.create')}</Button>
          </div>
        </div>
      </div>
    )
  },
})
