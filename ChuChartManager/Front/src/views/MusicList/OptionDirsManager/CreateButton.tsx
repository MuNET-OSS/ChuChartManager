import { defineComponent, ref } from "vue";
import { Button, Modal, NumberInput, showTransactionalDialog } from "@munet/ui";
import { createOptionDir } from "@/api/option";
import { updateOptionDirs, optionDirs } from "@/store/refs";
import { useI18n } from 'vue-i18n';

export default defineComponent({
  setup() {
    const show = ref(false);
    const { t } = useI18n();
    const id = ref(0);

    const setShow = () => {
      id.value = 1;
      show.value = true;
    };

    const save = async () => {
      if (id.value < 1 || id.value > 999) return;
      const dirName = `A${id.value.toString().padStart(3, '0')}`;
      if (optionDirs.value.find(d => d.dirName === dirName)) {
        await showTransactionalDialog(t('optionDir.dirExists'), '', undefined, true);
        return;
      }
      show.value = false;
      await createOptionDir(dirName);
      await updateOptionDirs();
    };

    return () => (
      <Button onClick={setShow}>
        {t('common.create')}

        <Modal
          width="min(30vw,25em)"
          title={t('optionDir.create')}
          v-model:show={show.value}
        >{{
          default: () =>
            <div class="flex gap-2 items-center">
              <span>A</span>
              <NumberInput v-model:value={id.value} class="w-full" min={1} max={999} />
            </div>,
          actions: () =>
            <Button onClick={save}>{t('common.confirm')}</Button>
        }}</Modal>
      </Button>
    );
  }
});
