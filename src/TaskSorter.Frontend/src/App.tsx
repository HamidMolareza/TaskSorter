import { startTransition, useDeferredValue, useEffect, useState } from 'react'
import {
  Database,
  GitPullRequest,
  KeyRound,
  Play,
  Plus,
  RefreshCcw,
  Save,
  Search,
  Trash2,
} from 'lucide-react'
import { api } from './api'
import type { ConfigPreview, ProfileDetail, ProfileDraft, ProfileSummary, SaveProfileRequest, TaskItem } from './types'

const emptyDraft: ProfileDraft = {
  name: 'New profile',
  repositoryLines: '',
  labelLines: '',
  taskLimit: 10,
  delayInMilliseconds: 500,
}

export function App() {
  const [profiles, setProfiles] = useState<ProfileSummary[]>([])
  const [selectedProfileId, setSelectedProfileId] = useState<string>('')
  const [draft, setDraft] = useState<ProfileDraft>(emptyDraft)
  const [token, setToken] = useState('')
  const [preview, setPreview] = useState<ConfigPreview | null>(null)
  const [tasks, setTasks] = useState<TaskItem[]>([])
  const [warnings, setWarnings] = useState<string[]>([])
  const [status, setStatus] = useState('Loading')
  const [error, setError] = useState('')
  const [isBusy, setIsBusy] = useState(false)
  const [repositoryFilter, setRepositoryFilter] = useState('all')
  const [statusFilter, setStatusFilter] = useState('all')
  const [typeFilter, setTypeFilter] = useState('all')
  const [tierFilter, setTierFilter] = useState('all')
  const [search, setSearch] = useState('')
  const deferredSearch = useDeferredValue(search)

  useEffect(() => {
    void loadProfiles()
  }, [])

  useEffect(() => {
    if (!selectedProfileId)
      return

    let ignore = false
    api.getProfile(selectedProfileId)
      .then((profile) => {
        if (!ignore)
          setDraft(profileToDraft(profile))
      })
      .catch((err: Error) => setError(err.message))

    return () => {
      ignore = true
    }
  }, [selectedProfileId])

  useEffect(() => {
    let ignore = false
    const handle = window.setTimeout(() => {
      if (!draft.repositoryLines && !draft.labelLines) {
        setPreview(null)
        return
      }

      api.previewConfig(draft)
        .then((result) => {
          if (!ignore)
            setPreview(result)
        })
        .catch((err: Error) => {
          if (!ignore)
            setError(err.message)
        })
    }, 250)

    return () => {
      ignore = true
      window.clearTimeout(handle)
    }
  }, [draft])

  async function loadProfiles() {
    setIsBusy(true)
    setError('')
    try {
      const result = await api.getProfiles()
      setProfiles(result)
      if (!selectedProfileId && result.length > 0)
        setSelectedProfileId(result[0].id)
      setStatus(`${result.length} profile${result.length === 1 ? '' : 's'}`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load profiles.')
    } finally {
      setIsBusy(false)
    }
  }

  async function saveProfile() {
    setIsBusy(true)
    setError('')
    try {
      const body = toSaveRequest(draft, token)
      const saved = draft.id
        ? await api.updateProfile(draft.id, body)
        : await api.createProfile(body)

      setToken('')
      setSelectedProfileId(saved.id)
      setDraft(profileToDraft(saved))
      await loadProfiles()
      setStatus('Saved')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save profile.')
    } finally {
      setIsBusy(false)
    }
  }

  async function deleteProfile() {
    if (!draft.id)
      return

    setIsBusy(true)
    setError('')
    try {
      await api.deleteProfile(draft.id)
      setSelectedProfileId('')
      setDraft(emptyDraft)
      setTasks([])
      await loadProfiles()
      setStatus('Deleted')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete profile.')
    } finally {
      setIsBusy(false)
    }
  }

  async function runProfile() {
    if (!draft.id)
      return

    setIsBusy(true)
    setError('')
    try {
      const result = await api.runProfile(draft.id)
      setTasks(result.items)
      setWarnings(result.warnings)
      setStatus(`${result.items.length} ranked tasks`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to run profile.')
    } finally {
      setIsBusy(false)
    }
  }

  function updateDraft(patch: Partial<ProfileDraft>) {
    startTransition(() => {
      setDraft((current) => ({ ...current, ...patch }))
    })
  }

  function createNewProfile() {
    setSelectedProfileId('')
    setDraft(emptyDraft)
    setToken('')
    setTasks([])
    setWarnings([])
  }

  const filteredTasks = filterTasks(tasks, {
    repository: repositoryFilter,
    status: statusFilter,
    type: typeFilter,
    tier: tierFilter,
    search: deferredSearch,
  })

  return (
    <main className="app-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">TaskSorter</p>
          <h1>Project queue</h1>
        </div>
        <div className="status-strip">
          <span>{status}</span>
          <button className="icon-button" onClick={() => void loadProfiles()} title="Refresh profiles" disabled={isBusy}>
            <RefreshCcw size={18} />
          </button>
        </div>
      </header>

      {error && <div className="alert danger">{error}</div>}
      {warnings.length > 0 && <div className="alert warning">{warnings.join(' ')}</div>}

      <section className="workspace">
        <aside className="panel profile-panel">
          <div className="panel-header">
            <h2>Profiles</h2>
            <button className="icon-button" onClick={createNewProfile} title="Create profile">
              <Plus size={18} />
            </button>
          </div>

          <select
            value={selectedProfileId}
            onChange={(event) => setSelectedProfileId(event.target.value)}
            aria-label="Profile"
          >
            <option value="">New profile</option>
            {profiles.map((profile) => (
              <option key={profile.id} value={profile.id}>
                {profile.name}
              </option>
            ))}
          </select>

          <label>
            Name
            <input value={draft.name} onChange={(event) => updateDraft({ name: event.target.value })} />
          </label>

          <div className="grid-two">
            <label>
              Top
              <input
                type="number"
                min={1}
                value={draft.taskLimit}
                onChange={(event) => updateDraft({ taskLimit: Number(event.target.value) })}
              />
            </label>
            <label>
              Delay
              <input
                type="number"
                min={0}
                value={draft.delayInMilliseconds}
                onChange={(event) => updateDraft({ delayInMilliseconds: Number(event.target.value) })}
              />
            </label>
          </div>

          <label>
            GitHub token
            <div className="input-with-icon">
              <KeyRound size={16} />
              <input
                type="password"
                value={token}
                onChange={(event) => setToken(event.target.value)}
                placeholder={currentProfile(profiles, draft.id)?.hasGitHubToken ? 'Stored' : 'Required'}
              />
            </div>
          </label>

          <div className="button-row">
            <button className="primary" onClick={() => void saveProfile()} disabled={isBusy} title="Save profile">
              <Save size={17} /> Save
            </button>
            <button onClick={() => void runProfile()} disabled={isBusy || !draft.id} title="Run profile">
              <Play size={17} /> Run
            </button>
            <button className="danger-button" onClick={() => void deleteProfile()} disabled={isBusy || !draft.id} title="Delete profile">
              <Trash2 size={17} />
            </button>
          </div>

          <div className="editors">
            <label>
              Repositories
              <textarea
                value={draft.repositoryLines}
                onChange={(event) => updateDraft({ repositoryLines: event.target.value })}
                spellCheck={false}
              />
            </label>
            <label>
              Labels
              <textarea
                value={draft.labelLines}
                onChange={(event) => updateDraft({ labelLines: event.target.value })}
                spellCheck={false}
              />
            </label>
          </div>
        </aside>

        <section className="main-grid">
          <section className="panel preview-panel">
            <div className="panel-header">
              <h2>Config preview</h2>
              <Database size={18} />
            </div>
            <Preview preview={preview} />
          </section>

          <section className="panel task-panel">
            <div className="panel-header">
              <h2>Ranked queue</h2>
              <GitPullRequest size={18} />
            </div>

            <div className="filters">
              <div className="search-box">
                <Search size={16} />
                <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search tasks" />
              </div>
              <FilterSelect label="Repository" value={repositoryFilter} values={unique(tasks.map((task) => task.repository))} onChange={setRepositoryFilter} />
              <FilterSelect label="Status" value={statusFilter} values={unique(tasks.map((task) => task.status))} onChange={setStatusFilter} />
              <FilterSelect label="Type" value={typeFilter} values={unique(tasks.map((task) => task.type))} onChange={setTypeFilter} />
              <FilterSelect label="Tier" value={tierFilter} values={unique(tasks.map((task) => task.projectTier))} onChange={setTierFilter} />
            </div>

            <TaskTable tasks={filteredTasks} />
          </section>
        </section>
      </section>
    </main>
  )
}

function Preview({ preview }: { preview: ConfigPreview | null }) {
  if (!preview)
    return <p className="empty">No profile selected.</p>

  return (
    <div className="preview-grid">
      {preview.errors.length > 0 && (
        <div className="alert danger compact">
          {preview.errors.map((error) => `${error.field}: ${error.message}`).join(' ')}
        </div>
      )}
      <div>
        <h3>Repositories</h3>
        <div className="mini-table">
          {preview.repositories.map((repository) => (
            <div key={`${repository.fullName}-${repository.tier}`}>
              <span>{repository.fullName}</span>
              <span>{repository.tier}</span>
              <strong>{repository.priorityScore}</strong>
            </div>
          ))}
        </div>
      </div>
      <div>
        <h3>Labels</h3>
        <div className="mini-table">
          {preview.labels.map((label) => (
            <div key={`${label.name}-${label.value}`}>
              <span>{label.name}</span>
              <strong>{label.value}</strong>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

function FilterSelect({ label, value, values, onChange }: {
  label: string
  value: string
  values: string[]
  onChange: (value: string) => void
}) {
  return (
    <label className="filter-control">
      {label}
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="all">All</option>
        {values.map((item) => (
          <option key={item} value={item}>{item}</option>
        ))}
      </select>
    </label>
  )
}

function TaskTable({ tasks }: { tasks: TaskItem[] }) {
  if (tasks.length === 0)
    return <p className="empty">No ranked tasks.</p>

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Rank</th>
            <th>Task</th>
            <th>Project</th>
            <th>State</th>
            <th>Score</th>
            <th>Breakdown</th>
          </tr>
        </thead>
        <tbody>
          {tasks.map((task) => (
            <tr key={task.id}>
              <td className="rank">{task.rank}</td>
              <td>
                <a href={task.url} target="_blank" rel="noreferrer">{task.title}</a>
                <div className="label-row">
                  {task.labels.slice(0, 6).map((label) => <span key={label}>{label}</span>)}
                </div>
              </td>
              <td>
                <strong>{task.repository}</strong>
                <small>{task.projectTier}</small>
              </td>
              <td>
                <span className="pill">{task.status}</span>
                <span className="pill muted">{task.size}</span>
                <span className="pill muted">{task.type}</span>
              </td>
              <td className="score">{task.score ?? 0}</td>
              <td className="breakdown">
                <span>R {task.scoreBreakdown.repository}</span>
                <span>L {task.scoreBreakdown.labels}</span>
                <span>S {task.scoreBreakdown.status}</span>
                <span>Z {task.scoreBreakdown.size}</span>
                <span>A {task.scoreBreakdown.assignment}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function profileToDraft(profile: ProfileDetail): ProfileDraft {
  return {
    id: profile.id,
    name: profile.name,
    repositoryLines: profile.repositoryLines,
    labelLines: profile.labelLines,
    taskLimit: profile.taskLimit,
    delayInMilliseconds: profile.delayInMilliseconds,
  }
}

function toSaveRequest(draft: ProfileDraft, token: string): SaveProfileRequest {
  return {
    ...draft,
    gitHubToken: token.trim() ? token : undefined,
  }
}

function currentProfile(profiles: ProfileSummary[], id?: string) {
  return profiles.find((profile) => profile.id === id)
}

function unique(values: string[]) {
  return Array.from(new Set(values.filter(Boolean))).sort((left, right) => left.localeCompare(right))
}

function filterTasks(tasks: TaskItem[], filter: {
  repository: string
  status: string
  type: string
  tier: string
  search: string
}) {
  var search = filter.search.trim().toLowerCase()
  return tasks.filter((task) => {
    if (filter.repository !== 'all' && task.repository !== filter.repository)
      return false
    if (filter.status !== 'all' && task.status !== filter.status)
      return false
    if (filter.type !== 'all' && task.type !== filter.type)
      return false
    if (filter.tier !== 'all' && task.projectTier !== filter.tier)
      return false
    if (search && !`${task.title} ${task.repository} ${task.labels.join(' ')}`.toLowerCase().includes(search))
      return false

    return true
  })
}
