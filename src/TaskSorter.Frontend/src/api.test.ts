import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from './api'

const defaultPriorityFactors = {
  repositoryTiers: {
    core: 500,
    active: 300,
    maintenance: 100,
    paused: -200,
    archive: -500,
  },
  assignmentBonus: 20,
  lockPenalty: -100,
}

describe('api', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sanitizes gateway HTML errors', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('<html><body><h1>504 Gateway Time-out</h1></body></html>', {
      status: 504,
      statusText: 'Gateway Time-out',
      headers: { 'Content-Type': 'text/html' },
    })))

    await expect(api.runProfile('11111111-1111-1111-1111-111111111111'))
      .rejects
      .toThrow('Request timed out while running the profile')
  })

  it('formats problem details returned by the API', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({
      title: 'Profile run timed out.',
      detail: 'The profile run exceeded the configured timeout.',
      status: 504,
    }), {
      status: 504,
      headers: { 'Content-Type': 'application/problem+json' },
    })))

    await expect(api.runProfile('11111111-1111-1111-1111-111111111111'))
      .rejects
      .toThrow('Profile run timed out. The profile run exceeded the configured timeout.')
  })

  it('sends refresh query when requested', async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({ items: [], warnings: [], cache: emptyCache, quota: healthyQuota }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)

    await api.runProfile('11111111-1111-1111-1111-111111111111', { refresh: true })

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/profiles/11111111-1111-1111-1111-111111111111/run?refresh=true',
      expect.objectContaining({ method: 'POST' }),
    )
  })

  it('sends quota override query when requested', async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({ items: [], warnings: [], cache: emptyCache, quota: healthyQuota }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)

    await api.runProfile('11111111-1111-1111-1111-111111111111', { refresh: true, quotaOverride: true })

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/profiles/11111111-1111-1111-1111-111111111111/run?refresh=true&quotaOverride=true',
      expect.objectContaining({ method: 'POST' }),
    )
  })

  it('sends current run body when provided', async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({ items: [], warnings: [], cache: emptyCache, quota: healthyQuota }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)

    await api.runProfile('11111111-1111-1111-1111-111111111111', {
      body: {
        repositoryLines: 'owner/repo core',
        labelLines: 'priority/high',
        taskLimit: 15,
        delayInMilliseconds: 0,
        priorityFactors: defaultPriorityFactors,
      },
    })

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/profiles/11111111-1111-1111-1111-111111111111/run',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          repositoryLines: 'owner/repo core',
          labelLines: 'priority/high',
          taskLimit: 15,
          delayInMilliseconds: 0,
          priorityFactors: defaultPriorityFactors,
        }),
      }),
    )
  })

  it('parses run progress stream and returns completed result', async () => {
    const events: string[] = []
    vi.stubGlobal('fetch', vi.fn(async () => new Response([
      JSON.stringify({ type: 'started', phase: 'starting', message: 'Starting.', completedOperations: 0, totalOperations: 0 }),
      JSON.stringify({ type: 'progress', phase: 'github', message: 'Loaded cache.', completedOperations: 1, totalOperations: 1, source: 'cache' }),
      JSON.stringify({ type: 'completed', phase: 'completed', message: 'Done.', completedOperations: 1, totalOperations: 1, quota: healthyQuota, result: { items: [], warnings: [], cache: emptyCache, quota: healthyQuota } }),
      '',
    ].join('\n'), {
      status: 200,
      headers: { 'Content-Type': 'application/x-ndjson' },
    })))

    const result = await api.runProfileWithProgress(
      '11111111-1111-1111-1111-111111111111',
      {
        repositoryLines: 'owner/repo core',
        labelLines: 'priority/high',
        taskLimit: 15,
        delayInMilliseconds: 0,
        priorityFactors: defaultPriorityFactors,
      },
      { onEvent: (event) => events.push(event.type) },
    )

    expect(events).toEqual(['started', 'progress', 'completed'])
    expect(result.cache.status).toBe('cache')
  })
})

const emptyCache = {
  status: 'cache',
  enabled: true,
  refreshRequested: false,
  durationSeconds: 300,
  hitCount: 1,
  gitHubRequestCount: 0,
  operationCount: 1,
  operations: [{ operation: 'repository-issues', target: 'owner/repo', source: 'cache' }],
}

const healthyQuota = {
  status: 'ok',
  protectionEnabled: true,
  reserveRequests: 50,
  warningRemaining: 250,
  estimatedRequiredRequests: 3,
  actualGitHubRequestCount: 0,
  limit: 5000,
  remaining: 4900,
  used: 100,
  resetAt: '2026-01-01T01:00:00Z',
  resetInSeconds: 3600,
  source: 'headers',
}
