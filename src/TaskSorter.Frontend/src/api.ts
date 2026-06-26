import type {
  ConfigPreview,
  ProfileDetail,
  ProfileDraft,
  ProfileSummary,
  RunProfileRequest,
  SaveProfileRequest,
  TaskRunProgressEvent,
  TaskRunQuota,
  TaskRunResponse,
} from './types'

type ProblemDetails = {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
  correlationId?: string
  retryAfterSeconds?: number
  quota?: TaskRunQuota
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
    ...init,
  })

  if (!response.ok) {
    throw new Error(await readErrorMessage(response))
  }

  if (response.status === 204)
    return undefined as T

  return response.json() as Promise<T>
}

async function streamRunRequest(
  path: string,
  body: RunProfileRequest,
  onEvent: (event: TaskRunProgressEvent) => void,
): Promise<TaskRunResponse> {
  const response = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

  if (!response.ok)
    throw new Error(await readErrorMessage(response))

  if (!response.body)
    throw new Error('The server did not return a progress stream.')

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let completedResult: TaskRunResponse | undefined

  while (true) {
    const { value, done } = await reader.read()
    buffer += decoder.decode(value, { stream: !done })
    const lines = buffer.split('\n')
    buffer = lines.pop() ?? ''

    for (const line of lines)
      completedResult = handleRunProgressLine(line, onEvent, completedResult)

    if (done)
      break
  }

  if (buffer.trim())
    completedResult = handleRunProgressLine(buffer, onEvent, completedResult)

  if (!completedResult)
    throw new Error('The run progress stream ended before a final result was returned.')

  return completedResult
}

function handleRunProgressLine(
  line: string,
  onEvent: (event: TaskRunProgressEvent) => void,
  completedResult: TaskRunResponse | undefined,
) {
  const trimmed = line.trim()
  if (!trimmed)
    return completedResult

  const event = JSON.parse(trimmed) as TaskRunProgressEvent
  onEvent(event)

  if (event.type === 'failed')
    throw new Error(event.error || event.message || 'Profile run failed.')

  return event.type === 'completed' && event.result
    ? event.result
    : completedResult
}

async function readErrorMessage(response: Response): Promise<string> {
  const fallback = `${response.status} ${response.statusText || 'Request failed'}`
  const body = (await response.text()).trim()
  const contentType = response.headers.get('content-type') ?? ''

  if (body && (contentType.includes('json') || body.startsWith('{'))) {
    try {
      return formatProblemDetails(JSON.parse(body) as ProblemDetails, fallback)
    } catch {
      return fallback
    }
  }

  if (body.startsWith('<')) {
    if (response.status === 504)
      return 'Request timed out while running the profile. Try again, reduce the repository count, or lower the delay.'

    return `${fallback}. The server returned an HTML error page.`
  }

  return body || fallback
}

function formatProblemDetails(problem: ProblemDetails, fallback: string): string {
  const errors = problem.errors
    ? Object.entries(problem.errors)
      .flatMap(([field, messages]) => messages.map((message) => `${field}: ${message}`))
      .join(' ')
    : ''

  const quota = problem.quota
  const quotaMessage = quota
    ? `Quota: ${quota.status}${quota.remaining === undefined ? '' : `, ${quota.remaining} remaining`}${quota.resetAt ? `, resets ${new Date(quota.resetAt).toLocaleString()}` : ''}.`
    : ''
  const retry = problem.retryAfterSeconds === undefined
    ? ''
    : `Retry after ${problem.retryAfterSeconds}s.`

  const message = [problem.title, problem.detail, quotaMessage, retry, errors]
    .filter(Boolean)
    .join(' ')

  return message || fallback
}

function runQuery(options?: { refresh?: boolean; quotaOverride?: boolean }) {
  const search = new URLSearchParams()
  if (options?.refresh)
    search.set('refresh', 'true')
  if (options?.quotaOverride)
    search.set('quotaOverride', 'true')

  const query = search.toString()
  return query ? `?${query}` : ''
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
        priorityFactors: draft.priorityFactors,
      }),
    }),
  runProfile: (id: string, options?: { refresh?: boolean; quotaOverride?: boolean; body?: RunProfileRequest }) =>
    request<TaskRunResponse>(`/api/profiles/${id}/run${runQuery(options)}`, {
      method: 'POST',
      body: options?.body ? JSON.stringify(options.body) : undefined,
    }),
  runProfileWithProgress: (
    id: string,
    body: RunProfileRequest,
    options: { refresh?: boolean; quotaOverride?: boolean; onEvent: (event: TaskRunProgressEvent) => void },
  ) =>
    streamRunRequest(
      `/api/profiles/${id}/run/stream${runQuery(options)}`,
      body,
      options.onEvent,
    ),
}
