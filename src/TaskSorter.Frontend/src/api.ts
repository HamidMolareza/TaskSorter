import type {
  ConfigPreview,
  ProfileDetail,
  ProfileDraft,
  ProfileSummary,
  SaveProfileRequest,
  TaskRunResponse,
} from './types'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
    ...init,
  })

  if (!response.ok) {
    const problem = await response.text()
    throw new Error(problem || `${response.status} ${response.statusText}`)
  }

  if (response.status === 204)
    return undefined as T

  return response.json() as Promise<T>
}

export const api = {
  getProfiles: () => request<ProfileSummary[]>('/api/profiles'),
  getProfile: (id: string) => request<ProfileDetail>(`/api/profiles/${id}`),
  createProfile: (body: SaveProfileRequest) =>
    request<ProfileDetail>('/api/profiles', {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  updateProfile: (id: string, body: SaveProfileRequest) =>
    request<ProfileDetail>(`/api/profiles/${id}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  deleteProfile: (id: string) =>
    request<void>(`/api/profiles/${id}`, {
      method: 'DELETE',
    }),
  previewConfig: (draft: ProfileDraft) =>
    request<ConfigPreview>('/api/preview-config', {
      method: 'POST',
      body: JSON.stringify({
        repositoryLines: draft.repositoryLines,
        labelLines: draft.labelLines,
        taskLimit: draft.taskLimit,
        delayInMilliseconds: draft.delayInMilliseconds,
      }),
    }),
  runProfile: (id: string) =>
    request<TaskRunResponse>(`/api/profiles/${id}/run`, {
      method: 'POST',
    }),
}
