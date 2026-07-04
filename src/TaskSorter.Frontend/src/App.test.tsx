import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { App } from './App'

const urgencyFactor = {
  id: '99999999-9999-9999-9999-999999999999',
  name: 'Urgency',
  description: 'How soon does this project need attention?',
  weight: 8,
  sortOrder: 0,
  rowVersion: 1,
  updatedAt: '2026-01-01T00:00:00Z',
}
const profile = {
  id: '11111111-1111-1111-1111-111111111111', name: 'Default', labelLines: 'priority/high', taskLimit: 10,
  delayInMilliseconds: 0, hasGitHubToken: true, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z',
  priorityFactors: { assignmentBonus: 20, lockPenalty: -100 },
  repositoryPriorityFactors: [urgencyFactor],
  repositories: [{
    id: '22222222-2222-2222-2222-222222222222', owner: 'owner', name: 'repo', fullName: 'owner/repo',
    repositoryTierId: '33333333-3333-3333-3333-333333333333', repositoryTierName: 'active', repositoryTierScore: 300,
    sortOrder: 0, rowVersion: 1, factorScore: 32, score: 332,
    ratings: [{ repositoryPriorityFactorId: urgencyFactor.id, rating: 4, rowVersion: 1 }],
    validation: [],
  }],
  labels: [
    { id: '55555555-5555-5555-5555-555555555555', name: 'priority/high', isIgnored: false, isPending: false, firstDiscoveredAt: '2026-01-01T00:00:00Z' },
    { id: '88888888-8888-8888-8888-888888888888', name: 'legacy/raw', isIgnored: true, isPending: false, firstDiscoveredAt: '2026-01-01T00:00:00Z' },
  ],
}
const alternateProfile = {
  ...profile,
  id: '66666666-6666-6666-6666-666666666666',
  name: 'Personal',
}
const profiles = [profile, alternateProfile]
const tiers = [
  { id: '33333333-3333-3333-3333-333333333333', name: 'active', score: 300, isDefault: true, assignedRepositoryCount: 1, rowVersion: 1, updatedAt: '2026-01-01T00:00:00Z' },
  { id: '44444444-4444-4444-4444-444444444444', name: 'paused', score: -200, isDefault: false, assignedRepositoryCount: 0, rowVersion: 1, updatedAt: '2026-01-01T00:00:00Z' },
]
const rankedTask = {
  rank: 1, id: 42, title: 'Fix cached ranking', type: 'issue', repository: 'owner/repo', projectTier: 'active',
  labels: ['priority/high', 'status/next', 'size/s', 'scope/bug', 'area/cache'],
  status: 'status/next', size: 'size/s', url: 'https://github.com/owner/repo/issues/42', assigned: true, locked: true,
  createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-02T00:00:00Z', score: 405,
  scoreBreakdown: { repository: 300, labels: 205, assignment: 0, lock: -100 },
  unscoredLabels: ['scope/bug'],
}
const runCache = { status: 'cache', enabled: true, refreshRequested: false, durationSeconds: 300, hitCount: 4, gitHubRequestCount: 0, operationCount: 1, operations: [] }
const runQuota = { status: 'ok', protectionEnabled: true, reserveRequests: 50, warningRemaining: 250, estimatedRequiredRequests: 1, actualGitHubRequestCount: 0, limit: 5000, remaining: 4900, source: 'snapshot' }

function json(body: unknown, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }) }
function streamJsonLines(events: unknown[]) { return new Response(events.map((event) => JSON.stringify(event)).join('\n') + '\n', { headers: { 'Content-Type': 'application/x-ndjson' } }) }

function stubApi(options: {
  discoverLabels?: () => Promise<Response> | Response
  updateRepository?: (body: { owner: string; name: string; repositoryTierId: string; rowVersion: number }) => Promise<Response> | Response
  profileResponse?: typeof profile
} = {}) {
  let currentTiers = tiers
  const activeProfile = options.profileResponse ?? profile
  vi.stubGlobal('fetch', vi.fn(async (path: string, init?: RequestInit) => {
    if (path === '/api/profiles') return json(profiles)
    if (path === '/api/repository-tiers') return json(currentTiers)
    if (path === `/api/repository-tiers/${tiers[1].id}/default` && init?.method === 'PUT') {
      currentTiers = currentTiers.map((tier) => ({ ...tier, isDefault: tier.id === tiers[1].id }))
      return json(currentTiers[1])
    }
    if (path === `/api/profiles/${profile.id}` && !init?.method) return json(activeProfile)
    if (path === `/api/profiles/${profile.id}` && init?.method === 'PUT') return json(activeProfile)
    if (path === `/api/profiles/${alternateProfile.id}` && !init?.method) return json(alternateProfile)
    if (path === `/api/profiles/${alternateProfile.id}` && init?.method === 'PUT') return json(alternateProfile)
    if (path === `/api/profiles/${profile.id}/repositories/${profile.repositories[0].id}` && init?.method === 'PUT') {
      const body = JSON.parse(String(init.body))
      if (options.updateRepository)
        return options.updateRepository(body)
      return json({ ...activeProfile.repositories[0], owner: body.owner, name: body.name, fullName: `${body.owner}/${body.name}`, repositoryTierId: body.repositoryTierId, rowVersion: 2 })
    }
    if (path === `/api/profiles/${profile.id}/repositories/${profile.repositories[0].id}/factor-ratings/${urgencyFactor.id}` && init?.method === 'PUT') {
      const body = JSON.parse(String(init.body))
      return json({ repositoryPriorityFactorId: urgencyFactor.id, rating: body.rating, rowVersion: 2 })
    }
    if (path === `/api/profiles/${profile.id}/repository-priority-factors` && init?.method === 'POST') return json({ ...urgencyFactor, id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', name: 'Need', description: '', weight: 5 }, 201)
    if (path === `/api/profiles/${profile.id}/labels/discover`) {
      if (options.discoverLabels)
        return options.discoverLabels()
      return json({
      labels: [
        profile.labels[0],
        { id: '77777777-7777-7777-7777-777777777777', name: 'new-label', isIgnored: false, isPending: true, firstDiscoveredAt: '2026-01-01T00:00:00Z' },
      ],
      newLabelCount: 1,
      removedLabelCount: 1,
      cache: { status: 'cache', enabled: true, refreshRequested: false, durationSeconds: 300, hitCount: 1, gitHubRequestCount: 0, operationCount: 1, operations: [] },
      quota: { status: 'ok', protectionEnabled: true, reserveRequests: 50, warningRemaining: 250, estimatedRequiredRequests: 1, actualGitHubRequestCount: 0, source: 'snapshot' },
      discoveredAt: '2026-01-01T00:00:00Z',
    })
    }
    if (path === `/api/profiles/${profile.id}/run/stream` && init?.method === 'POST') return streamJsonLines([
      { type: 'started', phase: 'starting', message: 'Starting profile run.', completedOperations: 0, totalOperations: 2 },
      { type: 'progress', phase: 'fetching', message: 'Reading owner/repo from cache.', completedOperations: 1, totalOperations: 2, operation: 'issues', target: 'owner/repo', source: 'cache' },
      { type: 'completed', phase: 'completed', message: 'Ranked 1 task.', completedOperations: 2, totalOperations: 2, result: { items: [rankedTask], warnings: ['Quota reserve is close.'], cache: runCache, quota: runQuota } },
    ])
    return json({})
  }))
}

describe('App', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    window.history.replaceState(null, '', '/')
  })

  it('shows repository factor ratings and calculated scores', async () => {
    stubApi()
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Repositories' }))
    expect(screen.getByDisplayValue('owner')).not.toBeNull()
    expect(screen.getByText('Urgency')).not.toBeNull()
    expect(screen.getByLabelText('Urgency rating for owner/repo')).not.toBeNull()
    expect(screen.getByText('332')).not.toBeNull()
    expect(screen.getByText('Valid')).not.toBeNull()
    expect(screen.queryByRole('button', { name: 'Drag owner/repo' })).toBeNull()
  })

  it('changes the theme preference from the header menu', async () => {
    const onThemePreferenceChange = vi.fn()
    stubApi()
    render(<App themePreference="system" onThemePreferenceChange={onThemePreferenceChange} />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())

    fireEvent.click(screen.getByRole('button', { name: 'Open theme menu (System)' }))
    expect(screen.getByRole('menuitem', { name: 'Light' })).not.toBeNull()
    expect(screen.getByRole('menuitem', { name: 'Dark' })).not.toBeNull()
    expect(screen.getByRole('menuitem', { name: 'System' })).not.toBeNull()
    fireEvent.click(screen.getByRole('menuitem', { name: 'Dark' }))

    expect(onThemePreferenceChange).toHaveBeenCalledWith('dark')
  })

  it('manages repository factors in a separate tab', async () => {
    stubApi()
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Factors' }))
    expect(screen.getByText('Repository factors')).not.toBeNull()
    expect(screen.getByDisplayValue('Urgency')).not.toBeNull()
    expect(screen.getByDisplayValue('How soon does this project need attention?').tagName).toBe('TEXTAREA')
  })

  it('shows local repository validation without sending an invalid update', async () => {
    stubApi()
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Repositories' }))

    fireEvent.change(screen.getByDisplayValue('repo'), { target: { value: 'bad/name' } })

    expect(screen.getByText('Use only letters, numbers, dots, dashes, or underscores.')).not.toBeNull()
    expect(fetch).not.toHaveBeenCalledWith(
      `/api/profiles/${profile.id}/repositories/${profile.repositories[0].id}`,
      expect.objectContaining({ method: 'PUT' }))
  })

  it('shows backend repository validation inline', async () => {
    stubApi({
      updateRepository: () => json({
        title: 'One or more validation errors occurred.',
        errors: { repository: ['This repository is already configured for the profile.'] },
      }, 400),
    })
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Repositories' }))

    fireEvent.change(screen.getByDisplayValue('repo'), { target: { value: 'duplicate' } })

    await waitFor(() => expect(screen.getByText('This repository is already configured for the profile.')).not.toBeNull())
    expect(screen.queryByText(/unexpected error/i)).toBeNull()
  })

  it('sorts repositories by score only when the sort button is clicked', async () => {
    const lowRepository = { ...profile.repositories[0], name: 'low', fullName: 'owner/low', factorScore: 8, score: 308, ratings: [{ repositoryPriorityFactorId: urgencyFactor.id, rating: 1, rowVersion: 1 }] }
    const highRepository = { ...profile.repositories[0], id: 'aaaaaaaa-2222-2222-2222-222222222222', name: 'high', fullName: 'owner/high', factorScore: 32, score: 332, ratings: [{ repositoryPriorityFactorId: urgencyFactor.id, rating: 4, rowVersion: 1 }] }
    stubApi({ profileResponse: { ...profile, repositories: [lowRepository, highRepository] } })
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Repositories' }))

    fireEvent.click(screen.getByRole('button', { name: 'Sort by score' }))
    expect(Boolean(screen.getByDisplayValue('high').compareDocumentPosition(screen.getByDisplayValue('low')) & Node.DOCUMENT_POSITION_FOLLOWING)).toBe(true)

    const comboboxes = screen.getAllByRole('combobox')
    fireEvent.mouseDown(comboboxes[comboboxes.length - 1])
    fireEvent.click(screen.getByRole('option', { name: '5' }))

    await waitFor(() => expect(screen.getByText('340')).not.toBeNull())
    expect(Boolean(screen.getByDisplayValue('high').compareDocumentPosition(screen.getByDisplayValue('low')) & Node.DOCUMENT_POSITION_FOLLOWING)).toBe(true)
  })

  it('keeps repository scoring scroll inside a sticky grid viewport', async () => {
    const extraFactors = Array.from({ length: 8 }, (_, index) => ({
      ...urgencyFactor,
      id: `99999999-9999-9999-9999-99999999999${index}`,
      name: `Factor ${index + 1}`,
      sortOrder: index + 1,
    }))
    stubApi({ profileResponse: { ...profile, repositoryPriorityFactors: [urgencyFactor, ...extraFactors] } })
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Repositories' }))

    const container = screen.getByTestId('repositories-table-container')
    const table = container.querySelector('table')
    const repositoryHeader = screen.getByTestId('repositories-sticky-repository-header')
    const tierHeader = screen.getByTestId('repositories-sticky-tier-header')
    const repositoryCell = screen.getByTestId(`repository-sticky-cell-${profile.repositories[0].id}`)

    expect(container.getAttribute('aria-label')).toBe('Repository scoring grid')
    expect(getComputedStyle(container).overflow).toBe('auto')
    expect(table?.className).toContain('MuiTable-stickyHeader')
    expect(getComputedStyle(repositoryHeader).position).toBe('sticky')
    expect(getComputedStyle(tierHeader).position).toBe('sticky')
    expect(getComputedStyle(repositoryCell).position).toBe('sticky')
    expect(screen.getByText('Factor 8')).not.toBeNull()
  })

  it('uses the Scoring tab for tiers and tuning without status or size controls', async () => {
    stubApi()
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Scoring' }))
    expect(screen.getByText('Repository tiers')).not.toBeNull()
    expect(screen.getByText('Tuning')).not.toBeNull()
    expect(screen.queryByText('Status labels')).toBeNull()
    expect(screen.queryByText('Size labels')).toBeNull()
  })

  it('sets a repository tier as the default from the Scoring tab', async () => {
    stubApi()
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Scoring' }))
    fireEvent.click(screen.getByRole('button', { name: 'Set default' }))
    await waitFor(() => expect(screen.getByText('Default tier updated')).not.toBeNull())
    expect(fetch).toHaveBeenCalledWith(
      `/api/repository-tiers/${tiers[1].id}/default`,
      expect.objectContaining({ method: 'PUT' }))
  })

  it('opens a confirmation dialog before deleting a repository', async () => {
    stubApi()
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Repositories' }))
    fireEvent.click(screen.getByRole('button', { name: 'Remove repository' }))
    expect(screen.getByText('Remove repository?')).not.toBeNull()
    expect(screen.getByRole('button', { name: 'Remove repository' })).not.toBeNull()
  })

  it('switches profiles from the header profile menu', async () => {
    stubApi()
    render(<App />)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Open profile menu' })).not.toBeNull())
    fireEvent.click(screen.getByRole('button', { name: 'Open profile menu' }))
    expect(screen.getByText('Current profile')).not.toBeNull()
    fireEvent.click(screen.getByText('Personal'))
    fireEvent.click(screen.getByRole('tab', { name: 'Settings' }))
    await waitFor(() => expect(screen.getByDisplayValue('Personal')).not.toBeNull())
  })

  it('keeps profile settings separate from ranked queue run controls', async () => {
    stubApi()
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    expect(screen.queryByRole('tab', { name: 'Profile' })).toBeNull()
    fireEvent.click(screen.getByRole('tab', { name: 'Settings' }))
    expect(screen.getByLabelText('Profile name')).not.toBeNull()
    expect(screen.getByRole('button', { name: 'Delete profile' })).not.toBeNull()
    expect(screen.queryByLabelText('Top')).toBeNull()
    fireEvent.click(screen.getByRole('tab', { name: 'Ranked Queue' }))
    expect(screen.getByLabelText('Top')).not.toBeNull()
    expect(screen.getByLabelText('Delay (ms)')).not.toBeNull()
    expect(screen.getByRole('button', { name: 'Run' })).not.toBeNull()
  })

  it('renders ranked queue rows with scan signals and expandable score details', async () => {
    stubApi()
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Ranked Queue' }))
    expect(screen.getByText('Run this profile to see ranked tasks.')).not.toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Run' }))
    await waitFor(() => expect(screen.getByText('Fix cached ranking')).not.toBeNull())
    expect(screen.getByText('Cache: cache')).not.toBeNull()
    expect(screen.getByText('0 GitHub requests')).not.toBeNull()
    expect(screen.getByText('Quota: 4900/5000')).not.toBeNull()
    expect(screen.getByText('1 warning')).not.toBeNull()
    expect(screen.getByText('#1')).not.toBeNull()
    expect(screen.getByText('owner/repo')).not.toBeNull()
    expect(screen.getByText('active')).not.toBeNull()
    expect(screen.getByText('Assigned')).not.toBeNull()
    expect(screen.getByText('Locked')).not.toBeNull()
    expect(screen.getByText('405')).not.toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Show details for Fix cached ranking' }))
    expect(screen.getByText('Score breakdown')).not.toBeNull()
    expect(screen.getByText('Repository 300')).not.toBeNull()
    expect(screen.getByText('Unscored scope/bug')).not.toBeNull()
    expect(screen.getByRole('link', { name: 'Open task' })).not.toBeNull()
  })

  it('reconciles labels from repositories and uses drag and move controls for one unique label order', async () => {
    stubApi()
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Labels' }))
    await waitFor(() => expect(screen.getByText('New labels')).not.toBeNull())
    expect(screen.getByText('new-label')).not.toBeNull()
    expect(screen.queryByText('legacy/raw')).toBeNull()
    expect(screen.getByText(/1 stale label removed/)).not.toBeNull()
    expect(screen.getByText('New')).not.toBeNull()
    expect(screen.getByRole('button', { name: 'Drag priority/high' })).not.toBeNull()
    expect(screen.getByRole('button', { name: 'Move priority/high down' })).not.toBeNull()
    expect(screen.queryByLabelText('Order for priority/high')).toBeNull()
  })

  it('shows progress while discovering labels', async () => {
    let resolveDiscovery: (response: Response) => void = () => {}
    stubApi({
      discoverLabels: () => new Promise<Response>((resolve) => { resolveDiscovery = resolve }),
    })
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Labels' }))
    fireEvent.click(screen.getByRole('button', { name: 'Discover labels' }))

    expect(screen.getByText('Discovering...')).not.toBeNull()
    expect(screen.getByText('Discovering labels from configured repositories...')).not.toBeNull()

    resolveDiscovery(json({
      labels: profile.labels,
      newLabelCount: 0,
      removedLabelCount: 0,
      cache: { status: 'cache', enabled: true, refreshRequested: false, durationSeconds: 300, hitCount: 1, gitHubRequestCount: 0, operationCount: 1, operations: [] },
      quota: { status: 'ok', protectionEnabled: true, reserveRequests: 50, warningRemaining: 250, estimatedRequiredRequests: 1, actualGitHubRequestCount: 0, source: 'snapshot' },
      discoveredAt: '2026-01-01T00:00:00Z',
    }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Discover labels' })).not.toBeNull())
  })

  it('clears labels after discovery when no repositories are configured', async () => {
    stubApi({
      profileResponse: { ...profile, repositories: [] },
      discoverLabels: () => json({
        labels: [],
        newLabelCount: 0,
        removedLabelCount: 2,
        cache: { status: 'disabled', enabled: false, refreshRequested: false, durationSeconds: 0, hitCount: 0, gitHubRequestCount: 0, operationCount: 0, operations: [] },
        quota: { status: 'unknown', protectionEnabled: true, reserveRequests: 0, warningRemaining: 0, estimatedRequiredRequests: 0, actualGitHubRequestCount: 0, source: 'unavailable' },
        discoveredAt: '2026-01-01T00:00:00Z',
      }),
    })
    render(<App />)
    await waitFor(() => expect(screen.getByText('Autosave enabled')).not.toBeNull())
    fireEvent.click(screen.getByRole('tab', { name: 'Labels' }))
    expect(screen.getByText('priority/high')).not.toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Discover labels' }))

    await waitFor(() => expect(screen.getByText('2 stale labels removed')).not.toBeNull())
    expect(screen.queryByText('priority/high')).toBeNull()
    expect(screen.queryByText('Add at least one repository before discovering labels.')).toBeNull()
  })

  it('restores and updates the selected tab through the URL', async () => {
    window.history.replaceState(null, '', '/?tab=labels')
    stubApi()
    render(<App />)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Discover labels' })).not.toBeNull())

    fireEvent.click(screen.getByRole('tab', { name: 'Settings' }))

    expect(window.location.search).toBe('?tab=settings')
    expect(screen.getByLabelText('Profile name')).not.toBeNull()
  })
})
