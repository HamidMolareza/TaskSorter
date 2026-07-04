import type {
  ProfileDetail, ProfileSummary, RunProfileRequest, SaveProfileRequest, SaveProfileRepositoryRequest,
  SaveRepositoryTierRequest, TaskRunProgressEvent, TaskRunQuota, TaskRunResponse, RepositoryTier,
  ProfileRepository, UpdateProfileRepositoryRequest, LabelDiscovery, ProfileLabel,
  UpdateProfileLabelRequest, RepositoryPriorityFactor, SaveRepositoryPriorityFactorRequest,
  RepositoryFactorRating, UpdateRepositoryFactorRatingRequest,
} from './types'

type ProblemDetails = {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
  quota?: TaskRunQuota
  traceId?: string
  correlationId?: string
  message?: string
}

export class ApiRequestError extends Error {
  constructor(message: string, readonly status: number, readonly body: unknown) {
    super(message)
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, { headers: { 'Content-Type': 'application/json', ...init?.headers }, ...init })
  if (!response.ok) {
    const { message, body } = await readError(response)
    throw new ApiRequestError(message, response.status, body)
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}

async function readError(response: Response): Promise<{ message: string; body: unknown }> {
  const fallback = `${response.status} ${response.statusText || 'Request failed'}`
  const body = (await response.text()).trim()
  if (body.startsWith('<'))
    return { message: response.status === 504 ? 'Request timed out while running the profile.' : `${fallback}. The server returned an HTML error page.`, body }
  try {
    const problem = JSON.parse(body) as ProblemDetails
    const validation = validationMessages(problem).join(' ')
    const title = validation && problem.title === 'One or more validation errors occurred.' ? '' : problem.title
    const trace = problem.correlationId || problem.traceId
    const message = [problem.message, title, problem.detail, validation].filter(Boolean).join(' ') || fallback
    const traceSuffix = trace ? ` Reference: ${trace}.` : ''
    return { message: `${message}${traceSuffix}`, body: problem }
  } catch {
    return { message: body || fallback, body }
  }
}

function validationMessages(problem: ProblemDetails) {
  return problem.errors ? Object.values(problem.errors).flat().filter(Boolean) : []
}

export function validationErrorsOf(reason: unknown): Record<string, string[]> {
  if (!(reason instanceof ApiRequestError) || typeof reason.body !== 'object' || reason.body === null)
    return {}
  const errors = (reason.body as ProblemDetails).errors
  return errors && typeof errors === 'object' ? errors : {}
}

export function validationMessagesOf(reason: unknown) {
  return Object.values(validationErrorsOf(reason)).flat().filter(Boolean)
}

async function streamRun(path: string, body: RunProfileRequest, onEvent: (event: TaskRunProgressEvent) => void) {
  const response = await fetch(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
  if (!response.ok) {
    const { message, body: errorBody } = await readError(response)
    throw new ApiRequestError(message, response.status, errorBody)
  }
  if (!response.body)
    throw new Error('The server did not return a progress stream.')
  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let result: TaskRunResponse | undefined
  while (true) {
    const { value, done } = await reader.read()
    buffer += decoder.decode(value, { stream: !done })
    const lines = buffer.split('\n')
    buffer = lines.pop() ?? ''
    for (const line of lines) {
      if (!line.trim()) continue
      const event = JSON.parse(line) as TaskRunProgressEvent
      onEvent(event)
      if (event.type === 'failed') throw new Error(event.error || event.message)
      if (event.result) result = event.result
    }
    if (done) break
  }
  if (!result) throw new Error('The run progress stream ended before a final result was returned.')
  return result
}

function runQuery(options?: { refresh?: boolean; quotaOverride?: boolean }) {
  const params = new URLSearchParams()
  if (options?.refresh) params.set('refresh', 'true')
  if (options?.quotaOverride) params.set('quotaOverride', 'true')
  return params.size ? `?${params}` : ''
}

export const api = {
  getProfiles: () => request<ProfileSummary[]>('/api/profiles'),
  getProfile: (id: string) => request<ProfileDetail>(`/api/profiles/${id}`),
  createProfile: (body: SaveProfileRequest) => request<ProfileDetail>('/api/profiles', { method: 'POST', body: JSON.stringify(body) }),
  updateProfile: (id: string, body: SaveProfileRequest) => request<ProfileDetail>(`/api/profiles/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteProfile: (id: string) => request<void>(`/api/profiles/${id}`, { method: 'DELETE' }),
  runProfile: (id: string, options?: { refresh?: boolean; quotaOverride?: boolean; body?: RunProfileRequest }) =>
    request<TaskRunResponse>(`/api/profiles/${id}/run${runQuery(options)}`, { method: 'POST', body: options?.body ? JSON.stringify(options.body) : undefined }),
  getRepositoryTiers: () => request<RepositoryTier[]>('/api/repository-tiers'),
  createRepositoryTier: (body: SaveRepositoryTierRequest) => request<RepositoryTier>('/api/repository-tiers', { method: 'POST', body: JSON.stringify(body) }),
  updateRepositoryTier: (id: string, body: SaveRepositoryTierRequest) => request<RepositoryTier>(`/api/repository-tiers/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  setDefaultRepositoryTier: (id: string) => request<RepositoryTier>(`/api/repository-tiers/${id}/default`, { method: 'PUT' }),
  deleteRepositoryTier: (id: string, reassignAssignedRepositories = false) => request<void>(`/api/repository-tiers/${id}?reassignAssignedRepositories=${reassignAssignedRepositories}`, { method: 'DELETE' }),
  createProfileRepository: (profileId: string, body: SaveProfileRepositoryRequest) => request<ProfileRepository>(`/api/profiles/${profileId}/repositories`, { method: 'POST', body: JSON.stringify(body) }),
  updateProfileRepository: (profileId: string, id: string, body: UpdateProfileRepositoryRequest) => request<ProfileRepository>(`/api/profiles/${profileId}/repositories/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteProfileRepository: (profileId: string, id: string) => request<void>(`/api/profiles/${profileId}/repositories/${id}`, { method: 'DELETE' }),
  createRepositoryPriorityFactor: (profileId: string, body: SaveRepositoryPriorityFactorRequest) => request<RepositoryPriorityFactor>(`/api/profiles/${profileId}/repository-priority-factors`, { method: 'POST', body: JSON.stringify(body) }),
  updateRepositoryPriorityFactor: (profileId: string, id: string, body: SaveRepositoryPriorityFactorRequest) => request<RepositoryPriorityFactor>(`/api/profiles/${profileId}/repository-priority-factors/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteRepositoryPriorityFactor: (profileId: string, id: string) => request<void>(`/api/profiles/${profileId}/repository-priority-factors/${id}`, { method: 'DELETE' }),
  reorderRepositoryPriorityFactors: (profileId: string, factorIds: string[]) => request<RepositoryPriorityFactor[]>(`/api/profiles/${profileId}/repository-priority-factors/order`, { method: 'PUT', body: JSON.stringify({ factorIds }) }),
  updateRepositoryFactorRating: (profileId: string, repositoryId: string, factorId: string, body: UpdateRepositoryFactorRatingRequest) =>
    request<RepositoryFactorRating>(`/api/profiles/${profileId}/repositories/${repositoryId}/factor-ratings/${factorId}`, { method: 'PUT', body: JSON.stringify(body) }),
  getProfileLabels: (profileId: string) => request<ProfileLabel[]>(`/api/profiles/${profileId}/labels`),
  discoverProfileLabels: (profileId: string, refresh = false) => request<LabelDiscovery>(`/api/profiles/${profileId}/labels/discover${refresh ? '?refresh=true' : ''}`, { method: 'POST' }),
  updateProfileLabel: (profileId: string, id: string, body: UpdateProfileLabelRequest) => request<ProfileLabel>(`/api/profiles/${profileId}/labels/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  reorderProfileLabels: (profileId: string, labelIds: string[]) => request<ProfileLabel[]>(`/api/profiles/${profileId}/labels/order`, { method: 'PUT', body: JSON.stringify({ labelIds }) }),
  runProfileWithProgress: (id: string, body: RunProfileRequest, options: { refresh?: boolean; quotaOverride?: boolean; onEvent: (event: TaskRunProgressEvent) => void }) => streamRun(`/api/profiles/${id}/run/stream${runQuery(options)}`, body, options.onEvent),
}
