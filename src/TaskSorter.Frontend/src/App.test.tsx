import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { App } from './App'

const defaultPriorityFactors = {
  repositoryTiers: {
    core: 500,
    active: 300,
    maintenance: 100,
    paused: -200,
    archive: -500,
  },
  status: {
    inProgress: 60,
    next: 50,
    waiting: -150,
    blocked: -200,
    default: 0,
  },
  size: {
    small: 30,
    medium: 15,
    large: -10,
    default: 0,
  },
  assignmentBonus: 20,
  lockPenalty: -100,
}

const profile = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Default',
  taskLimit: 10,
  delayInMilliseconds: 0,
  hasGitHubToken: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  repositoryLines: 'owner/repo core',
  labelLines: 'priority/high\nstatus/next\nsize/s',
  priorityFactors: defaultPriorityFactors,
}

describe('App', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString()

      if (url === '/api/profiles')
        return json([profile])

      if (url === `/api/profiles/${profile.id}`)
        return json(profile)

      if (url === '/api/preview-config')
        return json({
          repositories: [{ owner: 'owner', name: 'repo', fullName: 'owner/repo', tier: 'core', priorityScore: 511 }],
          labels: [{ name: 'priority/high', displayName: 'priority/high', value: 4 }],
          warnings: [],
          errors: [],
        })

      if (url === `/api/profiles/${profile.id}/run/stream`) {
        const body = init?.body ? JSON.parse(init.body.toString()) as { taskLimit?: number } : { taskLimit: 1 }
        const items = Array.from({ length: body.taskLimit ?? 1 }, (_, index) => ({
          ...task,
          id: index + 1,
          rank: index + 1,
          title: index === 0 ? task.title : `Fix queued task ${index + 1}`,
          url: `https://github.com/owner/repo/issues/${index + 1}`,
        }))

        return ndjson([
          { type: 'started', phase: 'starting', message: 'Starting run.', completedOperations: 0, totalOperations: 0 },
          { type: 'progress', phase: 'github', message: 'Loaded owner/repo from GitHub.', completedOperations: 1, totalOperations: 1, operation: 'repository-issues', target: 'owner/repo', source: 'github', itemCount: items.length },
          {
            type: 'completed',
            phase: 'completed',
            message: `Completed with ${items.length} ranked tasks.`,
            completedOperations: 1,
            totalOperations: 1,
            itemCount: items.length,
            result: {
              items,
              warnings: [],
              cache: {
                status: 'github',
                enabled: true,
                refreshRequested: false,
                durationSeconds: 300,
                hitCount: 0,
                gitHubRequestCount: 1,
                operationCount: 1,
                operations: [{ operation: 'repository-issues', target: 'owner/repo', source: 'github' }],
              },
              quota: healthyQuota,
            },
          },
        ])
      }

      return json({ items: [], warnings: [], cache: emptyCache, quota: healthyQuota })
    }))
  })

  it('loads the profile editor and repository tab preview', async () => {
    render(<App />)

    await waitFor(() => expect(screen.getByDisplayValue('Default')).not.toBeNull())
    expect(screen.getByRole('tab', { name: 'Repositories' })).not.toBeNull()

    fireEvent.click(screen.getByRole('tab', { name: 'Repositories' }))

    await waitFor(() => expect(screen.getByText('owner/repo')).not.toBeNull())
    expect(screen.getByRole('tab', { name: 'Ranked Queue' })).not.toBeNull()
  })

  it('shows cache status after a run', async () => {
    render(<App />)

    await waitFor(() => expect(screen.getByDisplayValue('Default')).not.toBeNull())
    fireEvent.click(screen.getByRole('button', { name: 'Run' }))

    await waitFor(() => expect(screen.getByText('Cache: fetched from GitHub')).not.toBeNull())
    expect(screen.getByText('Quota: healthy')).not.toBeNull()
    expect(screen.getByText('Fix failing build')).not.toBeNull()
  })

  it('runs with the current top value without saving first', async () => {
    const fetchMock = vi.mocked(fetch)
    render(<App />)

    await waitFor(() => expect(screen.getByDisplayValue('Default')).not.toBeNull())
    fireEvent.change(screen.getByLabelText('Top'), { target: { value: '15' } })
    fireEvent.click(screen.getByRole('button', { name: 'Run' }))

    await waitFor(() => expect(screen.getByText('15 ranked tasks')).not.toBeNull())
    expect(screen.getByText('Fix queued task 15')).not.toBeNull()
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/profiles/${profile.id}/run/stream`,
      expect.objectContaining({
        body: expect.stringMatching(/"taskLimit":15.*"priorityFactors"/),
      }),
    )
  })

  it('updates priority factors from the Priority tab before running', async () => {
    const fetchMock = vi.mocked(fetch)
    render(<App />)

    await waitFor(() => expect(screen.getByDisplayValue('Default')).not.toBeNull())
    fireEvent.change(screen.getByLabelText('Top'), { target: { value: '1' } })
    fireEvent.click(screen.getByRole('tab', { name: 'Priority' }))
    fireEvent.change(screen.getByLabelText('Assignment bonus'), { target: { value: '33' } })
    fireEvent.click(screen.getByRole('tab', { name: 'Profile' }))
    fireEvent.click(screen.getByRole('button', { name: 'Run' }))

    await waitFor(() => expect(screen.getByText('1 ranked task')).not.toBeNull())
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/profiles/${profile.id}/run/stream`,
      expect.objectContaining({
        body: expect.stringMatching(/"assignmentBonus":33/),
      }),
    )
  })
})

const task = {
  rank: 1,
  id: 1,
  title: 'Fix failing build',
  type: 'Issue',
  repository: 'owner/repo',
  projectTier: 'core',
  labels: ['priority/high', 'status/next', 'size/s'],
  status: 'next',
  size: 's',
  url: 'https://github.com/owner/repo/issues/1',
  assigned: true,
  locked: false,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-02T00:00:00Z',
  score: 42,
  scoreBreakdown: {
    repository: 10,
    labels: 20,
    status: 5,
    size: 3,
    assignment: 4,
    lock: 0,
  },
  unscoredLabels: [],
}

const emptyCache = {
  status: 'github',
  enabled: true,
  refreshRequested: false,
  durationSeconds: 300,
  hitCount: 0,
  gitHubRequestCount: 0,
  operationCount: 0,
  operations: [],
}

const healthyQuota = {
  status: 'ok',
  protectionEnabled: true,
  reserveRequests: 50,
  warningRemaining: 250,
  estimatedRequiredRequests: 3,
  actualGitHubRequestCount: 1,
  limit: 5000,
  remaining: 4900,
  used: 100,
  resetAt: '2026-01-01T01:00:00Z',
  resetInSeconds: 3600,
  source: 'headers',
}

function json(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

function ndjson(events: unknown[]) {
  return new Response(`${events.map((event) => JSON.stringify(event)).join('\n')}\n`, {
    status: 200,
    headers: { 'Content-Type': 'application/x-ndjson' },
  })
}
