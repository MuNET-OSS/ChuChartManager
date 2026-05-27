interface FileSystemFileHandle {
  getFile(): Promise<File>
  createWritable(): Promise<FileSystemWritableFileStream>
}

interface FileSystemDirectoryHandle {
  getDirectoryHandle(name: string, options?: { create?: boolean }): Promise<FileSystemDirectoryHandle>
  getFileHandle(name: string, options?: { create?: boolean }): Promise<FileSystemFileHandle>
  values(): AsyncIterableIterator<FileSystemDirectoryHandle | FileSystemFileHandle>
  readonly kind: 'directory'
  readonly name: string
}

interface FileSystemWritableFileStream extends WritableStream {
  write(data: BufferSource | Blob | string): Promise<void>
  close(): Promise<void>
}

interface ShowOpenFilePickerOptions {
  id?: string
  startIn?: string
  types?: { description?: string; accept: Record<string, string[]> }[]
  multiple?: boolean
}

interface ShowDirectoryPickerOptions {
  id?: string
  startIn?: string
  mode?: 'read' | 'readwrite'
}

interface Window {
  showOpenFilePicker(options?: ShowOpenFilePickerOptions): Promise<FileSystemFileHandle[]>
  showDirectoryPicker(options?: ShowDirectoryPickerOptions): Promise<FileSystemDirectoryHandle>
}
