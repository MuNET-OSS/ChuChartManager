import { apiClient } from '.'

export interface CourseListItem {
  id: number
  name: string
  difficulty: string
  difficultyId: number
  musicCount: number
  assetDir: string
  dataName: string
}

export interface CourseMusicInfo {
  type: number
  musicId: number
  musicName: string
  diffId: number
  diffName: string
}

export interface CourseDetail {
  id: number
  name: string
  dataName: string
  assetDir: string
  difficultyId: number
  difficulty: string
  ruleId: number
  ruleName: string
  rewardId: number
  rewardName: string
  reward2ndId: number
  reward2ndName: string
  teamOnly: boolean
  isMusicDuplicateAllowed: boolean
  conditionsCourseId: number
  conditionsCourseName: string
  conditionsText: string
  priority: number
  musics: CourseMusicInfo[]
}

export interface CreateCourseMusicDto {
  musicId: number
  musicName: string
  diffId: number
  diffName: string
  diffData: string
}

export interface CreateCourseDto {
  targetDir: string
  id: number
  name: string
  difficultyId: number
  difficulty: string
  ruleId: number
  musics: CreateCourseMusicDto[]
}

export interface SaveCourseDto {
  name: string
  difficultyId: number
  difficulty: string
  ruleId: number
  rewardId: number
  rewardName: string
  reward2ndId: number
  reward2ndName: string
  teamOnly: boolean
  isMusicDuplicateAllowed: boolean
  conditionsCourseId: number
  conditionsCourseName: string
  conditionsText: string
  priority: number
  musics: CreateCourseMusicDto[]
}

export async function getCourseList(source?: string): Promise<CourseListItem[]> {
  const { data } = await apiClient.get('/api/Course/GetCourseList', { params: source ? { source } : {} })
  return data
}

export async function getCourse(id: number, assetDir: string): Promise<CourseDetail> {
  const { data } = await apiClient.get('/api/Course/GetCourse', { params: { id, assetDir } })
  return data
}

export async function createCourse(dto: CreateCourseDto): Promise<void> {
  await apiClient.post('/api/Course/CreateCourse', dto)
}

export async function saveCourse(id: number, assetDir: string, dto: SaveCourseDto): Promise<void> {
  await apiClient.post(`/api/Course/SaveCourse?id=${id}&assetDir=${assetDir}`, dto)
}

export async function deleteCourse(id: number, assetDir: string): Promise<void> {
  await apiClient.post(`/api/Course/DeleteCourse?id=${id}&assetDir=${assetDir}`)
}
