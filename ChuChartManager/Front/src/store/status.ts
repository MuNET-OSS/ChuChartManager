import { ref } from 'vue'

export const statusText = ref('')

export function setStatus(text: string) {
  statusText.value = text
}
