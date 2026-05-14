import { computed, defineComponent, PropType, ref } from "vue";
import type { OptionDirInfo, ConflictEntry } from "@/api/option";
import { deleteOptionDir, toggleCustomMark, checkConflict } from "@/api/option";
import { Button, DropMenu, Modal, showTransactionalDialog, addToast, theme } from '@munet/ui';
import { selectedSource, updateOptionDirs } from "@/store/refs";
import { useI18n } from 'vue-i18n';

export default defineComponent({
  props: {
    dir: { type: Object as PropType<OptionDirInfo>, required: true },
    selected: { type: Boolean, default: false },
  },
  emits: ['select'],
  setup(props, { emit }) {
    const { t } = useI18n();
    const showConflict = ref(false);
    const conflicts = ref<ConflictEntry[]>([]);
    const checking = ref(false);

    const handleToggleCustom = async () => {
      try {
        await toggleCustomMark(props.dir.dirName);
        await updateOptionDirs();
      } catch (e: any) {
        addToast({ message: e.response?.data || e.message, type: 'error' });
      }
    };

    const handleDelete = async () => {
      const confirmed = await showTransactionalDialog(
        t('common.delete'),
        t('optionDir.deleteConfirm'),
        [{ text: t('common.confirm'), action: true }, { text: t('common.cancel'), action: false }]
      );
      if (!confirmed) return;
      try {
        await deleteOptionDir(props.dir.dirName);
        if (selectedSource.value === props.dir.dirName) {
          selectedSource.value = 'A000';
        }
        await updateOptionDirs();
      } catch (e: any) {
        addToast({ message: e.response?.data || e.message, type: 'error' });
      }
    };

    const handleCheckConflict = async () => {
      showConflict.value = true;
      checking.value = true;
      try {
        conflicts.value = await checkConflict(props.dir.dirName);
      } catch (e: any) {
        addToast({ message: e.response?.data || e.message, type: 'error' });
      } finally {
        checking.value = false;
      }
    };

    const options = computed(() => [
      {
        label: t('optionDir.checkConflict'),
        action: handleCheckConflict,
      },
      {
        label: props.dir.isCustom ? t('optionDir.unmarkCustom') : t('optionDir.markCustom'),
        action: handleToggleCustom,
      },
      {
        label: t('common.delete'),
        action: handleDelete,
      },
    ]);

    return () => (
      <div
        class={[
          "flex items-center gap-2 px-3 py-2 rounded-lg cursor-pointer",
          props.selected && theme.value.listItem, theme.value.listItemHover,
        ]}
        onClick={() => emit('select')}
      >
        <div class="grow-1 min-w-0">
          <div class="truncate">
            {props.dir.dirName}
          </div>
          <div class="text-xs op-50">
            {t('optionDir.musicCount', { count: props.dir.musicCount })}
          </div>
        </div>
        <div class="flex items-center gap-1 shrink-0" onClick={(e: Event) => e.stopPropagation()}>
          {props.dir.dirName !== 'A000' && (
            <DropMenu options={options.value} buttonText="">
              {{
                trigger: (toggle: (val?: boolean) => void) => (
                  <Button variant="secondary" onClick={() => toggle()}>
                    <span class="i-ic-baseline-more-vert text-lg" />
                  </Button>
                ),
              }}
            </DropMenu>
          )}
        </div>
        <Modal
          width="min(60vw,50em)"
          title={t('optionDir.conflictCheck')}
          v-model:show={showConflict.value}
        >
          {checking.value ? (
            <div class="text-center op-50 p-4">{t('optionDir.checking')}</div>
          ) : conflicts.value.length === 0 ? (
            <div class="text-center op-50 p-4">{t('optionDir.noConflict')}</div>
          ) : (
            <div class="of-y-auto max-h-70vh">
              <table class="w-full text-sm border-collapse">
                <thead>
                  <tr class="border-b border-neutral/20">
                    <th class="p-2 text-left">ID</th>
                    <th class="p-2 text-left">{t('optionDir.conflictName')}</th>
                    <th class="p-2 text-left">{t('optionDir.conflictWith')}</th>
                  </tr>
                </thead>
                <tbody>
                  {conflicts.value.map((row, i) => (
                    <tr key={i} class="border-b border-neutral/10">
                      <td class="p-2">{row.musicId}</td>
                      <td class="p-2">{row.musicName}</td>
                      <td class="p-2">{row.conflictDir}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Modal>
      </div>
    );
  }
});
