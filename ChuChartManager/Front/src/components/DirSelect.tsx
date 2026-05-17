import { defineComponent, ref, computed } from 'vue'
import { Button, Modal, NumberInput, Select, addToast } from '@munet/ui'
import type { SelectOption } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { optionDirs, updateOptionDirs } from '@/store/refs'
import { createOptionDir } from '@/api/option'

export default defineComponent({
  props: {
    value: { type: String, default: '' },
  },
  emits: ['update:value'],
  setup(props, { emit }) {
    const { t } = useI18n()
    const showCreate = ref(false)
    const newDirId = ref(1)

    const options = computed<SelectOption[]>(() =>
      optionDirs.value
        .filter(d => d.dirName !== 'A000')
        .map(d => ({ label: `${d.dirName} (${d.musicCount})`, value: d.dirName }))
    )

    async function handleCreate() {
      if (newDirId.value < 1 || newDirId.value > 999) return
      const dirName = `A${newDirId.value.toString().padStart(3, '0')}`
      if (optionDirs.value.find(d => d.dirName === dirName)) {
        addToast({ message: t('optionDir.dirExists'), type: 'error' })
        return
      }
      try {
        await createOptionDir(dirName)
        await updateOptionDirs()
        showCreate.value = false
        emit('update:value', dirName)
        addToast({ message: t('common.create') + ' ✓', type: 'success' })
      } catch (e: any) {
        addToast({ message: String(e?.response?.data || e?.message), type: 'error' })
      }
    }

    return () => (
      <>
        <div class="flex gap-2">
          <div class="flex-1">
            <Select value={props.value} options={options.value}
              onChange={(v: string) => emit('update:value', v)} />
          </div>
          <Button onClick={() => { showCreate.value = true; newDirId.value = 1 }}>
            <span class="i-mdi-plus text-3.5" />
          </Button>
        </div>
        <Modal
          show={showCreate.value}
          title={t('optionDir.create')}
          width="min(90vw, 20em)"
          onUpdateShow={(v) => { if (!v) showCreate.value = false }}
        >
          <div class="p-4 flex flex-col gap-3">
            <div class="flex gap-2 items-center">
              <span class="text-sm font-bold">A</span>
              <NumberInput v-model:value={newDirId.value} min={1} max={999} />
            </div>
            <div class="flex justify-end gap-2">
              <Button onClick={() => { showCreate.value = false }}>{t('common.cancel')}</Button>
              <Button onClick={handleCreate}>{t('common.create')}</Button>
            </div>
          </div>
        </Modal>
      </>
    )
  },
})
