import { defineComponent, ref, watch } from 'vue'
import { Popover } from '@munet/ui'
import { useI18n } from 'vue-i18n'
import { getIdConflicts } from '@/api'

export default defineComponent({
  props: {
    id: { type: Number, required: true },
    assetDir: { type: String, required: true },
  },
  setup(props) {
    const { t } = useI18n()
    const conflicts = ref<string[]>([])

    watch(() => [props.id, props.assetDir], async () => {
      conflicts.value = await getIdConflicts(props.id, props.assetDir)
    }, { immediate: true })

    return () => !!conflicts.value.length && (
      <Popover trigger="hover">
        {{
          trigger: () => <div class="text-#f0a020 i-mdi-alert-outline text-1.2em shrink-0" />,
          default: () => (
            <div class="flex flex-col gap-1">
              {t('music.idConflictWarning')}
              {conflicts.value.map(dir => <div key={dir} class="font-mono">{dir}</div>)}
            </div>
          ),
        }}
      </Popover>
    )
  },
})
