import { defineComponent, ref, computed, onMounted } from 'vue'
import { Button, Modal, TextInput, NumberInput, Select, addToast } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { optionDirs } from '@/store/refs'
import DirSelect from '@/components/DirSelect'
import {
  getPresetList, getPreset, savePreset, createPreset, deletePreset,
  type PresetListItem, type PresetDetail, type BonusEntry,
} from '@/api/loginBonus'

const CATEGORY_TYPES = [
  { id: 1, key: 'loginBonus.categoryRegular' },
  { id: 2, key: 'loginBonus.categoryCumulative' },
  { id: 3, key: 'loginBonus.categoryLimited' },
]

export default defineComponent({
  setup() {
    const { t } = useI18n()

    const presets = ref<PresetListItem[]>([])
    const selectedPreset = ref<PresetDetail | null>(null)
    const loading = ref(false)
    const saving = ref(false)

    const editName = ref('')
    const editDisabled = ref(false)
    const editBonuses = ref<BonusEntry[]>([])

    const showCreateModal = ref(false)
    const newPresetTargetDir = ref('')
    const newPresetId = ref(9000)
    const newPresetName = ref('')

    const showDeleteConfirm = ref(false)

    const categoryOptions = computed<SelectOption[]>(() =>
      CATEGORY_TYPES.map(c => ({ label: t(c.key), value: c.id }))
    )

    async function loadPresets() {
      loading.value = true
      try {
        presets.value = await getPresetList()
      } finally {
        loading.value = false
      }
    }

    async function selectPreset(item: PresetListItem) {
      try {
        const detail = await getPreset(item.id, item.assetDir)
        selectedPreset.value = detail
        editName.value = detail.name
        editDisabled.value = detail.disabled
        editBonuses.value = detail.bonuses.map(b => ({ ...b }))
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    async function handleSave() {
      if (!selectedPreset.value) return
      saving.value = true
      try {
        await savePreset(selectedPreset.value.id, selectedPreset.value.assetDir, {
          name: editName.value,
          disabled: editDisabled.value,
          bonuses: editBonuses.value,
        })
        addToast({ message: t('common.save') + ' ✓', type: 'success' })
        await loadPresets()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      } finally {
        saving.value = false
      }
    }

    async function handleDelete() {
      if (!selectedPreset.value) return
      try {
        await deletePreset(selectedPreset.value.id, selectedPreset.value.assetDir)
        selectedPreset.value = null
        showDeleteConfirm.value = false
        addToast({ message: t('common.delete') + ' ✓', type: 'success' })
        await loadPresets()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    async function handleCreate() {
      try {
        await createPreset({
          targetDir: newPresetTargetDir.value,
          id: newPresetId.value,
          name: newPresetName.value,
        })
        showCreateModal.value = false
        addToast({ message: t('common.create') + ' ✓', type: 'success' })
        await loadPresets()
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    function addBonus() {
      const maxId = editBonuses.value.reduce((max, b) => Math.max(max, b.id), 0)
      editBonuses.value.push({
        id: maxId + 10,
        name: '',
        presentId: 0,
        presentName: '',
        itemNum: 1,
        needLoginDayCount: editBonuses.value.length + 1,
        categoryType: 1,
        disabled: false,
      })
    }

    function removeBonus(index: number) {
      editBonuses.value.splice(index, 1)
    }

    function openCreateModal() {
      showCreateModal.value = true
      newPresetId.value = 9000
      newPresetName.value = ''
      const custom = optionDirs.value.filter(d => d.dirName !== 'A000')
      if (custom.length > 0 && !newPresetTargetDir.value)
        newPresetTargetDir.value = custom[0].dirName
    }

    onMounted(loadPresets)

    return () => (
      <div class="flex h-full">
        <div class="w-72 flex-shrink-0 border-r border-solid border-[oklch(0.9_0.02_var(--hue))] flex flex-col">
          <div class="flex items-center justify-between p-3 border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
            <h3 class="text-base font-bold m-0">{t('loginBonus.presets')}</h3>
            <Button onClick={openCreateModal}>
              <span class="i-mdi-plus text-4" />
            </Button>
          </div>
          <div class="flex-1 of-y-auto">
            {loading.value ? (
              <div class="text-center op-40 py-6">{t('common.loading')}</div>
            ) : presets.value.length === 0 ? (
              <div class="text-center op-40 py-6">{t('loginBonus.noPresets')}</div>
            ) : presets.value.map(p => (
              <div
                key={`${p.id}-${p.assetDir}`}
                class={[
                  'px-3 py-2.5 cursor-pointer border-b border-solid border-[oklch(0.93_0.01_var(--hue))] transition-colors',
                  selectedPreset.value?.id === p.id && selectedPreset.value?.assetDir === p.assetDir
                    ? 'bg-[oklch(0.92_0.05_var(--hue))]'
                    : 'hover:bg-[oklch(0.97_0.02_var(--hue))]',
                ]}
                onClick={() => selectPreset(p)}
              >
                <div class="text-sm font-medium truncate">{p.name}</div>
                <div class="text-xs op-40 mt-0.5 flex items-center gap-2">
                  <span>ID: {p.id}</span>
                  <span>·</span>
                  <span>{p.assetDir}</span>
                  <span>·</span>
                  <span>{t('loginBonus.bonusCount', { count: p.bonusCount })}</span>
                  {p.disabled && <span class="text-red-4">{t('loginBonus.disabled')}</span>}
                </div>
              </div>
            ))}
          </div>
        </div>

        <div class="flex-1 min-w-0 flex flex-col">
          {selectedPreset.value ? (
            <>
              <div class="flex items-center gap-3 p-4 border-b border-solid border-[oklch(0.9_0.02_var(--hue))]">
                <h2 class="text-lg font-bold m-0 flex-1 truncate">{t('loginBonus.editPreset')}</h2>
                <Button onClick={handleSave} disabled={saving.value}>
                  {saving.value ? t('common.loading') : t('common.save')}
                </Button>
                <Button onClick={() => { showDeleteConfirm.value = true }}>
                  {t('common.delete')}
                </Button>
              </div>

              <div class="flex-1 of-y-auto p-4">
                <div class="flex gap-4 mb-4">
                  <div class="flex-1">
                    <label class="text-xs op-50 mb-1 block">{t('loginBonus.presetName')}</label>
                    <TextInput v-model:value={editName.value} />
                  </div>
                  <div class="flex items-end gap-2">
                    <label class="text-xs flex items-center gap-1 cursor-pointer">
                      <input type="checkbox" checked={editDisabled.value} onChange={(e: Event) => { editDisabled.value = (e.target as HTMLInputElement).checked }} />
                      {t('loginBonus.disabled')}
                    </label>
                  </div>
                </div>

                <div class="flex items-center justify-between mb-3">
                  <h3 class="text-sm font-bold m-0 op-70">{t('loginBonus.bonusCount', { count: editBonuses.value.length })}</h3>
                  <Button onClick={addBonus}>
                    <span class="i-mdi-plus text-3.5 mr-1" />
                    {t('loginBonus.addBonus')}
                  </Button>
                </div>

                {editBonuses.value.map((bonus, i) => (
                  <div
                    key={i}
                    class="border border-solid border-[oklch(0.9_0.02_var(--hue))] rounded-xl p-3 mb-3"
                  >
                    <div class="flex items-center justify-between mb-2">
                      <span class="text-xs font-bold op-50">#{i + 1}</span>
                      <span
                        class="i-mdi-close text-4 op-30 hover:op-70 cursor-pointer hover:text-red-5 transition-colors"
                        onClick={() => removeBonus(i)}
                      />
                    </div>
                    <div class="grid grid-cols-2 gap-3">
                      <div>
                        <label class="text-xs op-50 mb-1 block">{t('loginBonus.bonusId')}</label>
                        <NumberInput v-model:value={bonus.id} min={0} />
                      </div>
                      <div>
                        <label class="text-xs op-50 mb-1 block">{t('loginBonus.bonusName')}</label>
                        <TextInput v-model:value={bonus.name} />
                      </div>
                      <div>
                        <label class="text-xs op-50 mb-1 block">{t('loginBonus.presentId')}</label>
                        <NumberInput v-model:value={bonus.presentId} min={0} />
                      </div>
                      <div>
                        <label class="text-xs op-50 mb-1 block">{t('loginBonus.presentName')}</label>
                        <TextInput v-model:value={bonus.presentName} />
                      </div>
                      <div>
                        <label class="text-xs op-50 mb-1 block">{t('loginBonus.itemNum')}</label>
                        <NumberInput v-model:value={bonus.itemNum} min={1} />
                      </div>
                      <div>
                        <label class="text-xs op-50 mb-1 block">{t('loginBonus.needLoginDays')}</label>
                        <NumberInput v-model:value={bonus.needLoginDayCount} min={1} />
                      </div>
                      <div>
                        <label class="text-xs op-50 mb-1 block">{t('loginBonus.categoryType')}</label>
                        <Select v-model:value={bonus.categoryType} options={categoryOptions.value} />
                      </div>
                      <div class="flex items-end">
                        <label class="text-xs flex items-center gap-1 cursor-pointer">
                          <input type="checkbox" checked={bonus.disabled} onChange={(e: Event) => { bonus.disabled = (e.target as HTMLInputElement).checked }} />
                          {t('loginBonus.disabled')}
                        </label>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </>
          ) : (
            <div class="flex-1 flex items-center justify-center op-30">
              <div class="text-center">
                <span class="i-mdi-gift text-16 block mb-2" />
                <span class="text-sm">{t('loginBonus.title')}</span>
              </div>
            </div>
          )}
        </div>

        <Modal
          show={showCreateModal.value}
          title={t('loginBonus.createPreset')}
          width="min(90vw, 28em)"
          onUpdateShow={(v) => { if (!v) showCreateModal.value = false }}
        >
          <div class="p-4 flex flex-col gap-3">
            <div>
              <label class="text-xs op-50 mb-1 block">{t('tools.targetDir')}</label>
              <DirSelect v-model:value={newPresetTargetDir.value} />
            </div>
            <div>
              <label class="text-xs op-50 mb-1 block">{t('loginBonus.presetId')}</label>
              <NumberInput v-model:value={newPresetId.value} min={1} />
            </div>
            <div>
              <label class="text-xs op-50 mb-1 block">{t('loginBonus.presetName')}</label>
              <TextInput v-model:value={newPresetName.value} />
            </div>
            <div class="flex justify-end gap-2 mt-2">
              <Button onClick={() => { showCreateModal.value = false }}>{t('common.cancel')}</Button>
              <Button onClick={handleCreate}>{t('common.create')}</Button>
            </div>
          </div>
        </Modal>

        <Modal
          show={showDeleteConfirm.value}
          title={t('loginBonus.deletePreset')}
          width="min(90vw, 24em)"
          onUpdateShow={(v) => { if (!v) showDeleteConfirm.value = false }}
        >
          <div class="p-2">
            <p class="text-sm mb-4">
              {t('loginBonus.deletePresetMessage', {
                name: selectedPreset.value?.name ?? '',
                id: selectedPreset.value?.id ?? 0,
              })}
            </p>
            <div class="flex justify-end gap-2">
              <Button onClick={() => { showDeleteConfirm.value = false }}>{t('common.cancel')}</Button>
              <Button onClick={handleDelete}>{t('common.delete')}</Button>
            </div>
          </div>
        </Modal>
      </div>
    )
  },
})
