export type ProfileSummary = {
  id: string
  name: string
  taskLimit: number
  delayInMilliseconds: number
  hasGitHubToken: boolean
  updatedAt: string
}

export type ProfileDetail = ProfileSummary & {
  repositoryLines: string
  labelLines: string
  createdAt: string
}

export type ProfileDraft = {
  id?: string
  name: string
  repositoryLines: string
  labelLines: string
  taskLimit: number
  delayInMilliseconds: number
}

export type SaveProfileRequest = ProfileDraft & {
  gitHubToken?: string
}

export type ValidationIssue = {
  field: string
  message: string
}

export type RepositoryPreview = {
  owner: string
  name: string
  fullName: string
  tier: string
  priorityScore: number
}

export type LabelPreview = {
  name: string
  displayName: string
  value: number
}

export type ConfigPreview = {
  repositories: RepositoryPreview[]
  labels: LabelPreview[]
  warnings: string[]
  errors: ValidationIssue[]
}

export type ScoreBreakdown = {
  repository: number
  labels: number
  status: number
  size: number
  assignment: number
  lock: number
}

export type TaskItem = {
  rank: number
  id: number
  title: string
  type: string
  repository: string
  projectTier: string
  labels: string[]
  status: string
  size: string
  url: string
  assigned: boolean
  locked: boolean
  createdAt: string
  updatedAt?: string
  score?: number
  scoreBreakdown: ScoreBreakdown
  unscoredLabels: string[]
}

export type TaskRunResponse = {
  items: TaskItem[]
  warnings: string[]
}
