import { defineComponent, PropType } from 'vue'
import { Popover } from '@munet/ui'
import { useI18n } from 'vue-i18n'

export default defineComponent({
  props: {
    problems: { type: Array as PropType<string[]>, required: true },
    inline: { type: Boolean, default: false },
  },
  setup(props) {
    const { t } = useI18n()
    const label = (code: string) => t(`problems.${code}`)

    return () => {
      if (!props.problems.length) return null

      if (props.inline) {
        return (
          <Popover trigger="hover">
            {{
              trigger: () => <div class="text-#f0a020 i-mdi-alert-circle-outline text-1.1em shrink-0" />,
              default: () => (
                <div class="flex flex-col gap-1">
                  {props.problems.map(p => <div key={p}>{label(p)}</div>)}
                </div>
              ),
            }}
          </Popover>
        )
      }

      return (
        <div class="flex flex-col gap-1 c-#d99000 bg-#f0a02018 rd p-2 text-sm">
          {props.problems.map(p => (
            <div key={p} class="flex items-center gap-1.5">
              <div class="i-mdi-alert-circle-outline shrink-0" />
              {label(p)}
            </div>
          ))}
        </div>
      )
    }
  },
})
