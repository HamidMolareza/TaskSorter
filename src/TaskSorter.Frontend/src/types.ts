export type ProfileSummary = {
  id: string
  name: string
  taskLimit: number
  delayInMilliseconds: number
  hasGitHubToken: boolean
  updatedAt: string
}

export type RepositoryTierPriorityFactors = {
  core: number
  active: number
  maintenance: number
  paused: number
  archive: number
}

export type StatusPriorityFactors = {
  inProgress: number
  next: number
  waiting: number
  blocked: number
  default: number
}

export type SizePriorityFactors = {
  small: number
  medium: number
  large: number
  default: number
}

export type TaskPriorityFactors = {
  repositoryTiers: RepositoryTierPriorityFactors
  status: StatusPriorityFactors
  size: SizePriorityFactors
  assignmentBonus: number
  lockPenalty: number
}

export type ProfileDetail = ProfileSummary & {
  repositoryLines: string
  labelLines: string
  priorityFactors: TaskPriorityFactors
  createdAt: string
}

export type ProfileDraft = {
  id?: string
  name: string
  repositoryLines: string
  labelLines: string
  taskLimit: number
  delayInMilliseconds: number
  priorityFactors: TaskPriorityFactors
}

export type SaveProfileRequest = ProfileDraft & {
  gitHubToken?: string
}

export type RunProfileRequest = {
  repositoryLines: string
  labelLines: string
  taskLimit: number
  delayInMilliseconds: number
  priorityFactors: TaskPriorityFactors
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

export type TaskRunCacheOperation = {
  operation: 'current-user' | 'current-user-issues' | 'repository-issues'
  target: string
  source: 'cache' | 'github' | 'refresh' | 'disabled'
}

export type TaskRunCache = {
  status: 'cache' | 'github' | 'mixed' | 'refreshed' | 'disabled'
  enabled: boolean
  refreshRequested: boolean
  durationSeconds: number
  hitCount: number
  gitHubRequestCount: number
  operationCount: number
  operations: TaskRunCacheOperation[]
}

export type TaskRunQuota = {
  status: 'unknown' | 'ok' | 'low' | 'protected' | 'exhausted' | 'secondary-limited'
  protectionEnabled: boolean
  reserveRequests: number
  warningRemaining: number
  estimatedRequiredRequests: number
  actualGitHubRequestCount: number
  limit?: number
  remaining?: number
  used?: number
  resetAt?: string
  resetInSeconds?: number
  source: 'snapshot' | 'headers' | 'rate-limit-endpoint' | 'unavailable'
}

export type TaskRunResponse = {
  items: TaskItem[]
  warnings: string[]
  cache: TaskRunCache
  quota: TaskRunQuota
}

export type TaskRunProgressEvent = {
  type: 'started' | 'progress' | 'completed' | 'failed'
  phase: string
  message: string
  completedOperations: number
  totalOperations: number
  operation?: string
  target?: string
  source?: 'cache' | 'github' | 'refresh' | 'disabled'
  itemCount?: number
  refreshRequested?: boolean
  profileName?: string
  quota?: TaskRunQuota
  result?: TaskRunResponse
  error?: string
  correlationId?: string
}
