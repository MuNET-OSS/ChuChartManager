import { defineConfig, presetIcons, presetTypography, presetAttributify, transformerVariantGroup, transformerDirectives } from 'unocss'
import presetUno from '@unocss/preset-wind3'

export default defineConfig({
  content: {
    filesystem: ['../../MuNET-UI/src/**/*.{ts,tsx}'],
  },
  // 语义色令牌（见 src/theme/tokens.sass），明暗模式由 CSS 变量切换
  theme: {
    colors: {
      ccm: {
        bg: 'var(--ccm-bg)',
        surface: 'var(--ccm-surface)',
        'surface-2': 'var(--ccm-surface-2)',
        hover: 'var(--ccm-hover)',
        active: 'var(--ccm-active)',
        line: 'var(--ccm-line)',
        'line-subtle': 'var(--ccm-line-subtle)',
        'line-strong': 'var(--ccm-line-strong)',
        text: 'var(--ccm-text)',
        dim: 'var(--ccm-text-dim)',
        faint: 'var(--ccm-text-faint)',
        accent: 'var(--ccm-accent)',
        'accent-hover': 'var(--ccm-accent-hover)',
        'accent-fg': 'var(--ccm-accent-fg)',
        'accent-soft': 'var(--ccm-accent-soft)',
        glass: 'var(--ccm-glass)',
      },
    },
  },
  presets: [
    presetUno(),
    presetTypography(),
    presetIcons(),
    presetAttributify(),
  ],
  transformers: [
    transformerDirectives({
      applyVariable: ['--at-apply'],
    }),
    transformerVariantGroup(),
  ],
})
