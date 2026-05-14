import { defineComponent, ref } from "vue";
import { Button, addToast } from "@munet/ui";
import { importLocalOptionDir } from "@/api/option";
import { updateOptionDirs } from "@/store/refs";
import { useI18n } from 'vue-i18n';

export default defineComponent({
  setup() {
    const importing = ref(false);
    const { t } = useI18n();

    const handleImport = async () => {
      importing.value = true;
      try {
        const result = await importLocalOptionDir();
        if (result.imported) {
          addToast({ message: `${t('optionDir.importSuccess')} ${result.dirName}`, type: 'success' });
          await updateOptionDirs();
        }
      } catch (e: any) {
        addToast({ message: e.response?.data || e.message, type: 'error' });
      } finally {
        importing.value = false;
      }
    };

    return () => (
      <Button onClick={handleImport} ing={importing.value}>
        {t('common.import')}
      </Button>
    );
  }
});
