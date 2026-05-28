const INVALID_RE = /[<>:"/\\|?*\x00-\x1f]/g

export function sanitizeFsSegment(name: string, fallback = 'unknown'): string {
  if (!name) return fallback
  const cleaned = name.replace(INVALID_RE, '_').trim().replace(/[. ]+$/, '')
  return cleaned || fallback
}
