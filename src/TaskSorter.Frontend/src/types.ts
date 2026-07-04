export type ProfileSummary = {
  id: string
  name: string
  taskLimit: number
  delayInMilliseconds: number
  hasGitHubToken: boolean
  updatedAt: string
}

export type TaskPriorityFactors = {
  assignmentBonus: number
  lockPenalty: number
}

export type RepositoryValidationIssue = {
  severity: 'error' | 'warning'
  message: string
}

export type ProfileRepository = {
  id: string
  owner: string
  name: string
  fullName: string
  repositoryTierId: string
  repositoryTierName: string
  repositoryTierScore: number
  sortOrder: number
  rowVersion: number
  factorScore: number
  score: number
  ratings: RepositoryFactorRating[]
  validation: RepositoryValidationIssue[]
}

export type RepositoryPriorityFactor = {
  id: string
  name: string
  description: string
  weight: number
  sortOrder: number
  rowVersion: number
  updatedAt: string
}

export type RepositoryFactorRating = {
  repositoryPriorityFactorId: string
  rating: number
  rowVersion: number
}

export type RepositoryTier = {
  id: string
  name: string
  score: number
  isDefault: boolean
  assignedRepositoryCount: number
  rowVersion: number
  updatedAt: string
}

export type ProfileDetail = ProfileSummary & {
  labelLines: string
  priorityFactors: TaskPriorityFactors
  createdAt: string
  repositoryPriorityFactors: RepositoryPriorityFactor[]
  repositories: ProfileRepository[]
  labels: ProfileLabel[]
}

export type ProfileLabel = {
  id: string
  name: string
  isIgnored: boolean
  isPending: boolean
  firstDiscoveredAt: string
  lastDiscoveredAt?: string
}

export type LabelDiscovery = {
  labels: ProfileLabel[]
  newLabelCount: number
  removedLabelCount: number
  cache: TaskRunCache
  quota: TaskRunQuota
  discoveredAt: string
}

export type ProfileDraft = {
  id?: string
  name: string
  labelLines: string
  taskLimit: number
  delayInMilliseconds: number
  priorityFactors: TaskPriorityFactors
  repositoryPriorityFactors: RepositoryPriorityFactor[]
  repositories: ProfileRepository[]
  labels: ProfileLabel[]
}

export type SaveProfileRequest = Omit<ProfileDraft, 'id' | 'repositories' | 'repositoryPriorityFactors' | 'labels'> & { gitHubToken?: string }
export type RunProfileRequest = Pick<ProfileDraft, 'labelLines' | 'taskLimit' | 'delayInMilliseconds' | 'priorityFactors'> & {
  repositoryLines?: string
}
export type SaveRepositoryTierRequest = { name: string; score: number; rowVersion?: number }
export type SaveProfileRepositoryRequest = { owner: string; name: string; repositoryTierId?: string }
export type UpdateProfileRepositoryRequest = { owner: string; name: string; repositoryTierId: string; rowVersion: number }
export type UpdateProfileLabelRequest = { isIgnored: boolean }
export type SaveRepositoryPriorityFactorRequest = { name: string; description: string; weight: number; rowVersion?: number }
export type UpdateRepositoryFactorRatingRequest = { rating: number; rowVersion: number }

export type ScoreBreakdown = { repository: number; labels: number; assignment: number; lock: number }
export type TaskItem = {
  rank: number; id: number; title: string; type: string; repository: string; projectTier: string
  labels: string[]; status: string; size: string; url: string; assigned: boolean; locked: boolean
  createdAt: string; updatedAt?: string; score?: number; scoreBreakdown: ScoreBreakdown; unscoredLabels: string[]
}

export type TaskRunCacheOperation = { operation: string; target: string; source: 'cache' | 'github' | 'refresh' | 'disabled' }
export type TaskRunCache = {
  status: 'cache' | 'github' | 'mixed' | 'refreshed' | 'disabled'; enabled: boolean; refreshRequested: boolean
  durationSeconds: number; hitCount: number; gitHubRequestCount: number; operationCount: number; operations: TaskRunCacheOperation[]
}
export type TaskRunQuota = {
  status: 'unknown' | 'ok' | 'low' | 'protected' | 'exhausted' | 'secondary-limited'; protectionEnabled: boolean
  reserveRequests: number; warningRemaining: number; estimatedRequiredRequests: number; actualGitHubRequestCount: number
  limit?: number; remaining?: number; used?: number; resetAt?: string; resetInSeconds?: number
  source: 'snapshot' | 'headers' | 'rate-limit-endpoint' | 'unavailable'
}
export type TaskRunResponse = { items: TaskItem[]; warnings: string[]; cache: TaskRunCache; quota: TaskRunQuota }
export type TaskRunProgressEvent = {
  type: 'started' | 'progress' | 'completed' | 'failed'; phase: string; message: string
  completedOperations: number; totalOperations: number; operation?: string; target?: string
  source?: 'cache' | 'github' | 'refresh' | 'disabled'; itemCount?: number; refreshRequested?: boolean
  profileName?: string; quota?: TaskRunQuota; result?: TaskRunResponse; error?: string; correlationId?: string
}
