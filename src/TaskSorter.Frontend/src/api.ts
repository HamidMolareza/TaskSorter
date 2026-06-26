import type {
  ProfileDetail, ProfileSummary, RunProfileRequest, SaveProfileRequest, SaveProfileRepositoryRequest,
  SaveRepositoryTierRequest, TaskRunProgressEvent, TaskRunQuota, TaskRunResponse, RepositoryTier,
  ProfileRepository, UpdateProfileRepositoryRequest, LabelDiscovery, ProfileLabel,
  UpdateProfileLabelRequest,
} from './types'

type ProblemDetails = { title?: string; detail?: string; status?: number; errors?: Record<string, string[]>; quota?: TaskRunQuota }

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, { headers: { 'Content-Type': 'application/json', ...init?.headers }, ...init })
  if (!response.ok)
    throw new Error(await readErrorMessage(response))
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}

async function readErrorMessage(response: Response): Promise<string> {
  const fallback = `${response.status} ${response.statusText || 'Request failed'}`
  const body = (await response.text()).trim()
  if (body.startsWith('<'))
    return response.status === 504 ? 'Request timed out while running the profile.' : `${fallback}. The server returned an HTML error page.`
  try {
    const problem = JSON.parse(body) as ProblemDetails
    const validation = problem.errors ? Object.values(problem.errors).flat().join(' ') : ''
    return [problem.title, problem.detail, validation].filter(Boolean).join(' ') || fallback
  } catch {
    return body || fallback
  }
}

async function streamRun(path: string, body: RunProfileRequest, onEvent: (event: TaskRunProgressEvent) => void) {
  const response = await fetch(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
  if (!response.ok)
    throw new Error(await readErrorMessage(response))
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
  reorderProfileRepositories: (profileId: string, repositoryIds: string[]) => request<void>(`/api/profiles/${profileId}/repositories/order`, { method: 'PUT', body: JSON.stringify({ repositoryIds }) }),
  getProfileLabels: (profileId: string) => request<ProfileLabel[]>(`/api/profiles/${profileId}/labels`),
  discoverProfileLabels: (profileId: string, refresh = false) => request<LabelDiscovery>(`/api/profiles/${profileId}/labels/discover${refresh ? '?refresh=true' : ''}`, { method: 'POST' }),
  updateProfileLabel: (profileId: string, id: string, body: UpdateProfileLabelRequest) => request<ProfileLabel>(`/api/profiles/${profileId}/labels/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  reorderProfileLabels: (profileId: string, labelIds: string[]) => request<ProfileLabel[]>(`/api/profiles/${profileId}/labels/order`, { method: 'PUT', body: JSON.stringify({ labelIds }) }),
  runProfileWithProgress: (id: string, body: RunProfileRequest, options: { refresh?: boolean; quotaOverride?: boolean; onEvent: (event: TaskRunProgressEvent) => void }) => streamRun(`/api/profiles/${id}/run/stream${runQuery(options)}`, body, options.onEvent),
}
