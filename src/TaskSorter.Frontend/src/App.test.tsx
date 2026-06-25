import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { App } from './App'

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
}

describe('App', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
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

      return json({ items: [], warnings: [] })
    }))
  })

  it('loads the profile editor and preview', async () => {
    render(<App />)

    await waitFor(() => expect(screen.getByDisplayValue('Default')).not.toBeNull())
    await waitFor(() => expect(screen.getByText('owner/repo')).not.toBeNull())
    expect(screen.getByText('Ranked queue')).not.toBeNull()
  })
})

function json(body: unknown) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  }))
}
