import { startTransition, useDeferredValue, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  InputAdornment,
  LinearProgress,
  MenuItem,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import {
  ChevronDown,
  Database,
  FolderGit2,
  Gauge,
  GitPullRequest,
  KeyRound,
  Play,
  Plus,
  RefreshCcw,
  Save,
  Search,
  Server,
  SlidersHorizontal,
  Tags,
  Trash2,
} from 'lucide-react'
import { api } from './api'
import type {
  ConfigPreview,
  LabelPreview,
  ProfileDetail,
  ProfileDraft,
  ProfileSummary,
  RunProfileRequest,
  RepositoryPreview,
  SaveProfileRequest,
  SizePriorityFactors,
  StatusPriorityFactors,
  TaskItem,
  TaskPriorityFactors,
  RepositoryTierPriorityFactors,
  TaskRunCache,
  TaskRunCacheOperation,
  TaskRunProgressEvent,
  TaskRunQuota,
} from './types'

const defaultPriorityFactors: TaskPriorityFactors = {
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

const emptyDraft: ProfileDraft = {
  name: 'New profile',
  repositoryLines: '',
  labelLines: '',
  taskLimit: 10,
  delayInMilliseconds: 500,
  priorityFactors: defaultPriorityFactors,
}

const tabs = ['Profile', 'Repositories', 'Labels', 'Priority', 'Ranked Queue', 'Cache'] as const
const progressTimelineLimit = 8

type RunProgressState = {
  status: 'running' | 'completed' | 'failed'
  startedAt: number
  refresh: boolean
  phase: string
  message: string
  completedOperations: number
  totalOperations: number
  latestEvent?: TaskRunProgressEvent
  events: TaskRunProgressEvent[]
}

type PriorityFactorsPatch = Partial<Omit<TaskPriorityFactors, 'repositoryTiers' | 'status' | 'size'>> & {
  repositoryTiers?: Partial<RepositoryTierPriorityFactors>
  status?: Partial<StatusPriorityFactors>
  size?: Partial<SizePriorityFactors>
}

export function App() {
  const [profiles, setProfiles] = useState<ProfileSummary[]>([])
  const [selectedProfileId, setSelectedProfileId] = useState<string>('')
  const [draft, setDraft] = useState<ProfileDraft>(emptyDraft)
  const [token, setToken] = useState('')
  const [preview, setPreview] = useState<ConfigPreview | null>(null)
  const [tasks, setTasks] = useState<TaskItem[]>([])
  const [warnings, setWarnings] = useState<string[]>([])
  const [cache, setCache] = useState<TaskRunCache | null>(null)
  const [quota, setQuota] = useState<TaskRunQuota | null>(null)
  const [status, setStatus] = useState('Loading')
  const [error, setError] = useState('')
  const [isBusy, setIsBusy] = useState(false)
  const [runProgress, setRunProgress] = useState<RunProgressState | null>(null)
  const [quotaOverrideDialogOpen, setQuotaOverrideDialogOpen] = useState(false)
  const [progressNow, setProgressNow] = useState(Date.now())
  const [activeTab, setActiveTab] = useState(0)
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
    if (runProgress?.status !== 'running')
      return

    const handle = window.setInterval(() => setProgressNow(Date.now()), 1000)
    return () => window.clearInterval(handle)
  }, [runProgress?.status])

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
      setWarnings([])
      setCache(null)
      setQuota(null)
      await loadProfiles()
      setStatus('Deleted')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete profile.')
    } finally {
      setIsBusy(false)
    }
  }

  async function runProfile(refresh = false, quotaOverride = false) {
    if (!draft.id)
      return

    setQuotaOverrideDialogOpen(false)
    const startedAt = Date.now()
    setIsBusy(true)
    setError('')
    setWarnings([])
    setProgressNow(startedAt)
    setRunProgress({
      status: 'running',
      startedAt,
      refresh,
      phase: 'starting',
      message: refresh ? 'Forcing fresh GitHub reads and updating cache.' : 'Starting run with current form values.',
      completedOperations: 0,
      totalOperations: 0,
      events: [],
    })
    setStatus(refresh ? 'Refreshing cache' : 'Running')
    try {
      const result = await api.runProfileWithProgress(draft.id, toRunRequest(draft), {
        refresh,
        quotaOverride,
        onEvent: (event) => {
          setProgressNow(Date.now())
          if (event.quota)
            setQuota(event.quota)
          if (event.result?.quota)
            setQuota(event.result.quota)
          setRunProgress((current) => mergeRunProgress(current, event, startedAt, refresh))
        },
      })
      setTasks(result.items)
      setWarnings(result.warnings)
      setCache(result.cache)
      setQuota(result.quota)
      setStatus(rankedTaskCountLabel(result.items.length))
      setRunProgress((current) => current
        ? {
            ...current,
            status: 'completed',
            phase: 'completed',
            message: `Completed with ${result.items.length} ranked task${result.items.length === 1 ? '' : 's'}.`,
            completedOperations: result.cache.operationCount,
            totalOperations: result.cache.operationCount,
          }
        : current)
      setActiveTab(4)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to run profile.')
      setRunProgress((current) => current
        ? {
            ...current,
            status: 'failed',
            phase: 'failed',
            message: err instanceof Error ? err.message : 'Failed to run profile.',
          }
        : current)
    } finally {
      setIsBusy(false)
    }
  }

  function requestRefreshRun() {
    if (shouldConfirmQuotaOverride(quota)) {
      setQuotaOverrideDialogOpen(true)
      return
    }

    void runProfile(true)
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
    setCache(null)
    setQuota(null)
    setActiveTab(0)
  }

  const filteredTasks = useMemo(() => filterTasks(tasks, {
    repository: repositoryFilter,
    status: statusFilter,
    type: typeFilter,
    tier: tierFilter,
    search: deferredSearch,
  }), [tasks, repositoryFilter, statusFilter, typeFilter, tierFilter, deferredSearch])

  return (
    <Box component="main" sx={{ minHeight: '100vh', px: { xs: 2, md: 3 }, py: 3 }}>
      <Stack spacing={2.5} sx={{ maxWidth: 1500, mx: 'auto' }}>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ justifyContent: 'space-between' }}>
          <Box>
            <Typography variant="overline" color="primary" sx={{ fontWeight: 800, letterSpacing: 0 }}>
              TaskSorter
            </Typography>
            <Typography variant="h1">Project queue</Typography>
          </Box>
          <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            <Chip label={status} variant="outlined" />
            {cache && <CacheStatusChip cache={cache} />}
            {quota && <QuotaStatusChip quota={quota} />}
            <Tooltip title="Refresh profiles">
              <span>
                <IconButton onClick={() => void loadProfiles()} disabled={isBusy}>
                  <RefreshCcw size={18} />
                </IconButton>
              </span>
            </Tooltip>
          </Stack>
        </Stack>

        {error && <Alert severity="error">{error}</Alert>}
        {warnings.length > 0 && <Alert severity="warning">{warnings.join(' ')}</Alert>}
        {runProgress && <RunProgressPanel progress={runProgress} now={progressNow} />}

        <Card variant="outlined">
          {isBusy && <LinearProgress />}
          <Tabs
            value={activeTab}
            onChange={(_, value) => setActiveTab(value)}
            variant="scrollable"
            scrollButtons="auto"
            sx={{ borderBottom: 1, borderColor: 'divider', px: 1 }}
          >
            {tabs.map((tab) => (
              <Tab key={tab} label={tab} />
            ))}
          </Tabs>
        </Card>

        {activeTab === 0 && (
          <ProfileTab
            profiles={profiles}
            selectedProfileId={selectedProfileId}
            draft={draft}
            token={token}
            isBusy={isBusy}
            cache={cache}
            onSelectProfile={setSelectedProfileId}
            onCreateNew={createNewProfile}
            onUpdateDraft={updateDraft}
            onTokenChange={setToken}
            onSave={() => void saveProfile()}
            onDelete={() => void deleteProfile()}
            onRun={() => void runProfile()}
            onRefreshRun={requestRefreshRun}
          />
        )}

        {activeTab === 1 && (
          <RepositoriesTab
            value={draft.repositoryLines}
            preview={preview}
            onChange={(value) => updateDraft({ repositoryLines: value })}
          />
        )}

        {activeTab === 2 && (
          <LabelsTab
            value={draft.labelLines}
            preview={preview}
            onChange={(value) => updateDraft({ labelLines: value })}
          />
        )}

        {activeTab === 3 && (
          <PriorityTab
            priorityFactors={draft.priorityFactors}
            preview={preview}
            onChange={(value) => updateDraft({ priorityFactors: value })}
          />
        )}

        {activeTab === 4 && (
          <RankedQueueTab
            tasks={tasks}
            filteredTasks={filteredTasks}
            search={search}
            repositoryFilter={repositoryFilter}
            statusFilter={statusFilter}
            typeFilter={typeFilter}
            tierFilter={tierFilter}
            onSearch={setSearch}
            onRepositoryFilter={setRepositoryFilter}
            onStatusFilter={setStatusFilter}
            onTypeFilter={setTypeFilter}
            onTierFilter={setTierFilter}
          />
        )}

        {activeTab === 5 && (
          <CachePanel
            cache={cache}
            quota={quota}
            canRun={Boolean(draft.id)}
            isBusy={isBusy}
            onForceRefresh={requestRefreshRun}
          />
        )}

        <Dialog open={quotaOverrideDialogOpen} onClose={() => setQuotaOverrideDialogOpen(false)} maxWidth="sm" fullWidth>
          <DialogTitle>Confirm quota override</DialogTitle>
          <DialogContent>
            <Stack spacing={1.5} sx={{ pt: 0.5 }}>
              {quota && <QuotaStatusAlert quota={quota} />}
              <Typography color="text.secondary">
                Force refresh will bypass cache and may spend GitHub requests while quota is low. Continue only when fresh GitHub data is required.
              </Typography>
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setQuotaOverrideDialogOpen(false)}>Cancel</Button>
            <Button variant="contained" color="warning" onClick={() => void runProfile(true, true)}>
              Override and refresh
            </Button>
          </DialogActions>
        </Dialog>
      </Stack>
    </Box>
  )
}

function ProfileTab(props: {
  profiles: ProfileSummary[]
  selectedProfileId: string
  draft: ProfileDraft
  token: string
  isBusy: boolean
  cache: TaskRunCache | null
  onSelectProfile: (id: string) => void
  onCreateNew: () => void
  onUpdateDraft: (patch: Partial<ProfileDraft>) => void
  onTokenChange: (token: string) => void
  onSave: () => void
  onDelete: () => void
  onRun: () => void
  onRefreshRun: () => void
}) {
  const storedToken = currentProfile(props.profiles, props.draft.id)?.hasGitHubToken

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2.5}>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ justifyContent: 'space-between' }}>
            <SectionTitle icon={<Database size={20} />} title="Profile" />
            <Button variant="outlined" startIcon={<Plus size={17} />} onClick={props.onCreateNew}>
              New profile
            </Button>
          </Stack>

          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', md: '1.2fr 1fr 1fr' } }}>
            <TextField
              select
              label="Profile"
              value={props.selectedProfileId}
              onChange={(event) => props.onSelectProfile(event.target.value)}
              fullWidth
            >
              <MenuItem value="">New profile</MenuItem>
              {props.profiles.map((profile) => (
                <MenuItem key={profile.id} value={profile.id}>
                  {profile.name}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              label="Name"
              value={props.draft.name}
              onChange={(event) => props.onUpdateDraft({ name: event.target.value })}
              fullWidth
            />
            <TextField
              label="GitHub token"
              type="password"
              value={props.token}
              onChange={(event) => props.onTokenChange(event.target.value)}
              placeholder={storedToken ? 'Stored' : 'Required'}
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <KeyRound size={17} />
                    </InputAdornment>
                  ),
                },
              }}
              fullWidth
            />
          </Box>

          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
            <TextField
              label="Top"
              type="number"
              value={props.draft.taskLimit}
              slotProps={{ htmlInput: { min: 1 } }}
              onChange={(event) => props.onUpdateDraft({ taskLimit: Number(event.target.value) })}
            />
            <TextField
              label="Delay"
              type="number"
              value={props.draft.delayInMilliseconds}
              slotProps={{ htmlInput: { min: 0 } }}
              onChange={(event) => props.onUpdateDraft({ delayInMilliseconds: Number(event.target.value) })}
            />
          </Box>

          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
            <Button variant="contained" startIcon={<Save size={17} />} onClick={props.onSave} disabled={props.isBusy}>
              Save
            </Button>
            <Button variant="outlined" startIcon={<Play size={17} />} onClick={props.onRun} disabled={props.isBusy || !props.draft.id}>
              Run
            </Button>
            <Button variant="outlined" startIcon={<RefreshCcw size={17} />} onClick={props.onRefreshRun} disabled={props.isBusy || !props.draft.id}>
              Refresh
            </Button>
            <Button color="error" variant="outlined" startIcon={<Trash2 size={17} />} onClick={props.onDelete} disabled={props.isBusy || !props.draft.id}>
              Delete
            </Button>
          </Stack>

          {props.cache && (
            <Alert severity={cacheAlertSeverity(props.cache.status)} icon={<Server size={18} />}>
              Last run used {cacheStatusLabel(props.cache.status)} with {props.cache.hitCount} cache hit(s) and {props.cache.gitHubRequestCount} GitHub request(s).
            </Alert>
          )}
        </Stack>
      </CardContent>
    </Card>
  )
}

function RepositoriesTab({ value, preview, onChange }: {
  value: string
  preview: ConfigPreview | null
  onChange: (value: string) => void
}) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2.5}>
          <SectionTitle icon={<FolderGit2 size={20} />} title="Repositories" />
          <TextField
            label="Repository priority lines"
            value={value}
            onChange={(event) => onChange(event.target.value)}
            multiline
            minRows={12}
            spellCheck={false}
            fullWidth
            sx={{ '& textarea': { fontFamily: '"JetBrains Mono", "Cascadia Code", monospace' } }}
          />
          <ConfigMessages preview={preview} />
          <RepositoryPreviewTable repositories={preview?.repositories ?? []} />
        </Stack>
      </CardContent>
    </Card>
  )
}

function LabelsTab({ value, preview, onChange }: {
  value: string
  preview: ConfigPreview | null
  onChange: (value: string) => void
}) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2.5}>
          <SectionTitle icon={<Tags size={20} />} title="Labels" />
          <TextField
            label="Label priority lines"
            value={value}
            onChange={(event) => onChange(event.target.value)}
            multiline
            minRows={12}
            spellCheck={false}
            fullWidth
            sx={{ '& textarea': { fontFamily: '"JetBrains Mono", "Cascadia Code", monospace' } }}
          />
          <ConfigMessages preview={preview} />
          <LabelPreviewTable labels={preview?.labels ?? []} />
        </Stack>
      </CardContent>
    </Card>
  )
}

function PriorityTab(props: {
  priorityFactors: TaskPriorityFactors
  preview: ConfigPreview | null
  onChange: (value: TaskPriorityFactors) => void
}) {
  const update = (patch: PriorityFactorsPatch) => props.onChange({
    ...props.priorityFactors,
    ...patch,
    repositoryTiers: patch.repositoryTiers
      ? { ...props.priorityFactors.repositoryTiers, ...patch.repositoryTiers }
      : props.priorityFactors.repositoryTiers,
    status: patch.status
      ? { ...props.priorityFactors.status, ...patch.status }
      : props.priorityFactors.status,
    size: patch.size
      ? { ...props.priorityFactors.size, ...patch.size }
      : props.priorityFactors.size,
  })

  const updateRepositoryTier = (patch: Partial<RepositoryTierPriorityFactors>) =>
    update({ repositoryTiers: patch })
  const updateStatus = (patch: Partial<StatusPriorityFactors>) =>
    update({ status: patch })
  const updateSize = (patch: Partial<SizePriorityFactors>) =>
    update({ size: patch })

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2.5}>
          <SectionTitle icon={<SlidersHorizontal size={20} />} title="Priority" />
          <ConfigMessages preview={props.preview} />
          <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 1 }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Factor</TableCell>
                  <TableCell align="right">Value</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                <FactorGroupRow label="Repository tiers" />
                <FactorRow
                  label="Core"
                  value={props.priorityFactors.repositoryTiers.core}
                  onChange={(value) => updateRepositoryTier({ core: value })}
                />
                <FactorRow
                  label="Active"
                  value={props.priorityFactors.repositoryTiers.active}
                  onChange={(value) => updateRepositoryTier({ active: value })}
                />
                <FactorRow
                  label="Maintenance"
                  value={props.priorityFactors.repositoryTiers.maintenance}
                  onChange={(value) => updateRepositoryTier({ maintenance: value })}
                />
                <FactorRow
                  label="Paused"
                  value={props.priorityFactors.repositoryTiers.paused}
                  onChange={(value) => updateRepositoryTier({ paused: value })}
                />
                <FactorRow
                  label="Archive"
                  value={props.priorityFactors.repositoryTiers.archive}
                  onChange={(value) => updateRepositoryTier({ archive: value })}
                />

                <FactorGroupRow label="Status labels" />
                <FactorRow
                  label="In progress"
                  value={props.priorityFactors.status.inProgress}
                  onChange={(value) => updateStatus({ inProgress: value })}
                />
                <FactorRow
                  label="Next"
                  value={props.priorityFactors.status.next}
                  onChange={(value) => updateStatus({ next: value })}
                />
                <FactorRow
                  label="Waiting"
                  value={props.priorityFactors.status.waiting}
                  onChange={(value) => updateStatus({ waiting: value })}
                />
                <FactorRow
                  label="Blocked"
                  value={props.priorityFactors.status.blocked}
                  onChange={(value) => updateStatus({ blocked: value })}
                />
                <FactorRow
                  label="Default status"
                  value={props.priorityFactors.status.default}
                  onChange={(value) => updateStatus({ default: value })}
                />

                <FactorGroupRow label="Size labels" />
                <FactorRow
                  label="Small"
                  value={props.priorityFactors.size.small}
                  onChange={(value) => updateSize({ small: value })}
                />
                <FactorRow
                  label="Medium"
                  value={props.priorityFactors.size.medium}
                  onChange={(value) => updateSize({ medium: value })}
                />
                <FactorRow
                  label="Large"
                  value={props.priorityFactors.size.large}
                  onChange={(value) => updateSize({ large: value })}
                />
                <FactorRow
                  label="Default size"
                  value={props.priorityFactors.size.default}
                  onChange={(value) => updateSize({ default: value })}
                />

                <FactorGroupRow label="Tuning" />
                <FactorRow
                  label="Assignment bonus"
                  value={props.priorityFactors.assignmentBonus}
                  onChange={(value) => update({ assignmentBonus: value })}
                />
                <FactorRow
                  label="Lock penalty"
                  value={props.priorityFactors.lockPenalty}
                  onChange={(value) => update({ lockPenalty: value })}
                />
              </TableBody>
            </Table>
          </TableContainer>
        </Stack>
      </CardContent>
    </Card>
  )
}

function RankedQueueTab(props: {
  tasks: TaskItem[]
  filteredTasks: TaskItem[]
  search: string
  repositoryFilter: string
  statusFilter: string
  typeFilter: string
  tierFilter: string
  onSearch: (value: string) => void
  onRepositoryFilter: (value: string) => void
  onStatusFilter: (value: string) => void
  onTypeFilter: (value: string) => void
  onTierFilter: (value: string) => void
}) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2.5}>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ justifyContent: 'space-between' }}>
            <SectionTitle icon={<GitPullRequest size={20} />} title="Ranked queue" />
            <Chip label={`${props.filteredTasks.length} visible / ${props.tasks.length} total`} variant="outlined" />
          </Stack>

          <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', md: '1.4fr repeat(4, 1fr)' } }}>
            <TextField
              label="Search tasks"
              value={props.search}
              onChange={(event) => props.onSearch(event.target.value)}
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <Search size={17} />
                    </InputAdornment>
                  ),
                },
              }}
            />
            <FilterSelect label="Repository" value={props.repositoryFilter} values={unique(props.tasks.map((task) => task.repository))} onChange={props.onRepositoryFilter} />
            <FilterSelect label="Status" value={props.statusFilter} values={unique(props.tasks.map((task) => task.status))} onChange={props.onStatusFilter} />
            <FilterSelect label="Type" value={props.typeFilter} values={unique(props.tasks.map((task) => task.type))} onChange={props.onTypeFilter} />
            <FilterSelect label="Tier" value={props.tierFilter} values={unique(props.tasks.map((task) => task.projectTier))} onChange={props.onTierFilter} />
          </Box>

          <TaskTable tasks={props.filteredTasks} />
        </Stack>
      </CardContent>
    </Card>
  )
}

function RunProgressPanel({ progress, now }: { progress: RunProgressState; now: number }) {
  const percent = progress.totalOperations > 0
    ? Math.min(100, Math.round((progress.completedOperations / progress.totalOperations) * 100))
    : undefined
  const elapsedSeconds = Math.max(0, Math.floor((now - progress.startedAt) / 1000))

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2}>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ justifyContent: 'space-between' }}>
            <SectionTitle icon={<Play size={20} />} title={progress.refresh ? 'Refresh progress' : 'Run progress'} />
            <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
              <Chip label={progress.status} color={progress.status === 'failed' ? 'error' : progress.status === 'completed' ? 'success' : 'primary'} />
              <Chip label={`${elapsedSeconds}s elapsed`} variant="outlined" />
              {progress.totalOperations > 0 && (
                <Chip label={`${progress.completedOperations}/${progress.totalOperations} operations`} variant="outlined" />
              )}
            </Stack>
          </Stack>

          <Box>
            <LinearProgress
              variant={percent === undefined ? 'indeterminate' : 'determinate'}
              value={percent}
              color={progress.status === 'failed' ? 'error' : progress.status === 'completed' ? 'success' : 'primary'}
            />
          </Box>

          <Alert severity={progress.status === 'failed' ? 'error' : progress.status === 'completed' ? 'success' : 'info'}>
            {progress.message}
          </Alert>

          {progress.latestEvent && (
            <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', md: 'repeat(5, 1fr)' } }}>
              <Metric label="Phase" value={progress.latestEvent.phase} />
              <Metric label="Source" value={progress.latestEvent.source ? sourceLabel(progress.latestEvent.source) : 'pending'} />
              <Metric label="Target" value={progress.latestEvent.target ?? 'profile'} />
              <Metric label="Items" value={progress.latestEvent.itemCount?.toString() ?? '-'} />
              <Metric label="Quota" value={progress.latestEvent.quota ? quotaStatusLabel(progress.latestEvent.quota.status) : 'pending'} />
            </Box>
          )}

          {progress.events.length > 0 && (
            <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 1 }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Step</TableCell>
                    <TableCell>Detail</TableCell>
                    <TableCell>Source</TableCell>
                    <TableCell align="right">Progress</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {progress.events.map((event, index) => (
                    <TableRow key={`${event.type}-${event.phase}-${event.target ?? 'profile'}-${index}`}>
                      <TableCell>{event.phase}</TableCell>
                      <TableCell>{event.message}</TableCell>
                      <TableCell>{event.source ? <Chip size="small" color={cacheSourceColor(event.source)} label={sourceLabel(event.source)} /> : '-'}</TableCell>
                      <TableCell align="right">
                        {event.totalOperations > 0 ? `${event.completedOperations}/${event.totalOperations}` : '-'}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </Stack>
      </CardContent>
    </Card>
  )
}

function CachePanel(props: {
  cache: TaskRunCache | null
  quota: TaskRunQuota | null
  canRun: boolean
  isBusy: boolean
  onForceRefresh: () => void
}) {
  const forceRefreshButton = (
    <Button
      variant="outlined"
      startIcon={<RefreshCcw size={17} />}
      onClick={props.onForceRefresh}
      disabled={props.isBusy || !props.canRun}
    >
      Clear cache and refresh
    </Button>
  )

  if (!props.cache) {
    return (
      <Card variant="outlined">
        <CardContent>
          <Stack spacing={2}>
            <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ justifyContent: 'space-between' }}>
              <SectionTitle icon={<Server size={20} />} title="Cache and quota" />
              {forceRefreshButton}
            </Stack>
            <Alert severity="info">Run a profile to see GitHub cache status.</Alert>
            {props.quota && <QuotaStatusAlert quota={props.quota} />}
          </Stack>
        </CardContent>
      </Card>
    )
  }

  const cache = props.cache
  const groups = groupCacheOperations(cache.operations)

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2.5}>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ justifyContent: 'space-between' }}>
            <SectionTitle icon={<Server size={20} />} title="Cache and quota" />
            <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
              <CacheStatusChip cache={cache} />
              {props.quota && <QuotaStatusChip quota={props.quota} />}
              {forceRefreshButton}
            </Stack>
          </Stack>

          <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', md: 'repeat(4, 1fr)' } }}>
            <Metric label="TTL" value={`${cache.durationSeconds}s`} />
            <Metric label="Cache hits" value={cache.hitCount.toString()} />
            <Metric label="GitHub reads" value={cache.gitHubRequestCount.toString()} />
            <Metric label="Operations" value={cache.operationCount.toString()} />
          </Box>

          <Alert severity={cacheAlertSeverity(cache.status)}>
            {cache.refreshRequested
              ? 'Refresh bypassed existing cache entries and stored fresh successful GitHub responses.'
              : `Last run used ${cacheStatusLabel(cache.status)}.`}
          </Alert>

          {props.quota && (
            <Stack spacing={1.5}>
              <QuotaStatusAlert quota={props.quota} />
              <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', md: 'repeat(4, 1fr)' } }}>
                <Metric label="Remaining" value={formatQuotaNumber(props.quota.remaining, props.quota.limit)} />
                <Metric label="Reserve" value={props.quota.reserveRequests.toString()} />
                <Metric label="Estimated need" value={props.quota.estimatedRequiredRequests.toString()} />
                <Metric label="Actual GitHub reads" value={props.quota.actualGitHubRequestCount.toString()} />
                <Metric label="Reset" value={formatQuotaReset(props.quota)} />
                <Metric label="Quota source" value={quotaSourceLabel(props.quota.source)} />
                <Metric label="Used" value={props.quota.used?.toString() ?? '-'} />
                <Metric label="Warning at" value={props.quota.warningRemaining.toString()} />
              </Box>
            </Stack>
          )}

          <Stack spacing={1}>
            {Object.entries(groups).map(([source, operations]) => (
              <Accordion key={source} disableGutters variant="outlined">
                <AccordionSummary expandIcon={<ChevronDown size={18} />}>
                  <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                    <Chip size="small" color={cacheSourceColor(source)} label={sourceLabel(source)} />
                    <Typography>{operations.length} operation{operations.length === 1 ? '' : 's'}</Typography>
                  </Stack>
                </AccordionSummary>
                <AccordionDetails>
                  <CacheOperationTable operations={operations} />
                </AccordionDetails>
              </Accordion>
            ))}
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  )
}

function RepositoryPreviewTable({ repositories }: { repositories: RepositoryPreview[] }) {
  if (repositories.length === 0)
    return <Alert severity="info">No repositories parsed yet.</Alert>

  return (
    <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 1 }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Repository</TableCell>
            <TableCell>Tier</TableCell>
            <TableCell align="right">Priority</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {repositories.map((repository) => (
            <TableRow key={`${repository.fullName}-${repository.tier}`}>
              <TableCell>{repository.fullName}</TableCell>
              <TableCell><Chip size="small" label={repository.tier} /></TableCell>
              <TableCell align="right">{repository.priorityScore}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  )
}

function LabelPreviewTable({ labels }: { labels: LabelPreview[] }) {
  if (labels.length === 0)
    return <Alert severity="info">No labels parsed yet.</Alert>

  return (
    <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 1 }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Label</TableCell>
            <TableCell align="right">Value</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {labels.map((label) => (
            <TableRow key={`${label.name}-${label.value}`}>
              <TableCell>{label.displayName}</TableCell>
              <TableCell align="right">{label.value}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  )
}

function TaskTable({ tasks }: { tasks: TaskItem[] }) {
  if (tasks.length === 0)
    return <Alert severity="info">No ranked tasks.</Alert>

  return (
    <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 1, overflowX: 'auto' }}>
      <Table size="small" sx={{ minWidth: 980 }}>
        <TableHead>
          <TableRow>
            <TableCell>Rank</TableCell>
            <TableCell>Task</TableCell>
            <TableCell>Project</TableCell>
            <TableCell>State</TableCell>
            <TableCell align="right">Score</TableCell>
            <TableCell>Breakdown</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {tasks.map((task) => (
            <TableRow key={task.id} hover>
              <TableCell sx={{ fontWeight: 800 }}>{task.rank}</TableCell>
              <TableCell>
                <Stack spacing={1}>
                  <Typography component="a" href={task.url} target="_blank" rel="noreferrer" sx={{ color: 'secondary.main', fontWeight: 700, textDecoration: 'none' }}>
                    {task.title}
                  </Typography>
                  <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: 'wrap' }}>
                    {task.labels.slice(0, 6).map((label) => <Chip key={label} size="small" label={label} />)}
                  </Stack>
                </Stack>
              </TableCell>
              <TableCell>
                <Typography sx={{ fontWeight: 700 }}>{task.repository}</Typography>
                <Typography variant="caption" color="text.secondary">{task.projectTier}</Typography>
              </TableCell>
              <TableCell>
                <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: 'wrap' }}>
                  <Chip size="small" color="primary" variant="outlined" label={task.status} />
                  <Chip size="small" variant="outlined" label={task.size} />
                  <Chip size="small" variant="outlined" label={task.type} />
                </Stack>
              </TableCell>
              <TableCell align="right" sx={{ fontWeight: 800 }}>{task.score ?? 0}</TableCell>
              <TableCell>
                <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: 'wrap' }}>
                  <Chip size="small" label={`Repo ${task.scoreBreakdown.repository}`} />
                  <Chip size="small" label={`Labels ${task.scoreBreakdown.labels}`} />
                  <Chip size="small" label={`Status ${task.scoreBreakdown.status}`} />
                  <Chip size="small" label={`Size ${task.scoreBreakdown.size}`} />
                  <Chip size="small" label={`Assigned ${task.scoreBreakdown.assignment}`} />
                  <Chip size="small" label={`Locked ${task.scoreBreakdown.lock}`} />
                </Stack>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  )
}

function CacheOperationTable({ operations }: { operations: TaskRunCacheOperation[] }) {
  return (
    <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 1 }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Operation</TableCell>
            <TableCell>Target</TableCell>
            <TableCell>Source</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {operations.map((operation, index) => (
            <TableRow key={`${operation.operation}-${operation.target}-${index}`}>
              <TableCell>{operation.operation}</TableCell>
              <TableCell>{operation.target}</TableCell>
              <TableCell><Chip size="small" color={cacheSourceColor(operation.source)} label={sourceLabel(operation.source)} /></TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  )
}

function FilterSelect({ label, value, values, onChange }: {
  label: string
  value: string
  values: string[]
  onChange: (value: string) => void
}) {
  return (
    <TextField select label={label} value={value} onChange={(event) => onChange(event.target.value)}>
      <MenuItem value="all">All</MenuItem>
      {values.map((item) => (
        <MenuItem key={item} value={item}>{item}</MenuItem>
      ))}
    </TextField>
  )
}

function ConfigMessages({ preview }: { preview: ConfigPreview | null }) {
  if (!preview)
    return null

  return (
    <Stack spacing={1}>
      {preview.errors.length > 0 && (
        <Alert severity="error">
          {preview.errors.map((error) => `${error.field}: ${error.message}`).join(' ')}
        </Alert>
      )}
      {preview.warnings.length > 0 && <Alert severity="warning">{preview.warnings.join(' ')}</Alert>}
    </Stack>
  )
}

function SectionTitle({ icon, title }: { icon: ReactNode; title: string }) {
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
      {icon}
      <Typography variant="h2">{title}</Typography>
    </Stack>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <Box sx={{ border: 1, borderColor: 'divider', borderRadius: 1, p: 1.5 }}>
      <Typography variant="caption" color="text.secondary">{label}</Typography>
      <Typography variant="h6" sx={{ lineHeight: 1.2 }}>{value}</Typography>
    </Box>
  )
}

function CacheStatusChip({ cache }: { cache: TaskRunCache }) {
  return (
    <Chip
      color={cacheStatusColor(cache.status)}
      label={`Cache: ${cacheStatusLabel(cache.status)}`}
      variant={cache.status === 'disabled' ? 'outlined' : 'filled'}
    />
  )
}

function QuotaStatusChip({ quota }: { quota: TaskRunQuota }) {
  return (
    <Chip
      icon={<Gauge size={16} />}
      color={quotaStatusColor(quota.status)}
      label={`Quota: ${quotaStatusLabel(quota.status)}`}
      variant={quota.status === 'unknown' ? 'outlined' : 'filled'}
    />
  )
}

function QuotaStatusAlert({ quota }: { quota: TaskRunQuota }) {
  return (
    <Alert severity={quotaAlertSeverity(quota.status)} icon={<Gauge size={18} />}>
      {quotaDetail(quota)}
    </Alert>
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
    priorityFactors: profile.priorityFactors,
  }
}

function toSaveRequest(draft: ProfileDraft, token: string): SaveProfileRequest {
  return {
    ...draft,
    gitHubToken: token.trim() ? token : undefined,
  }
}

function toRunRequest(draft: ProfileDraft): RunProfileRequest {
  return {
    repositoryLines: draft.repositoryLines,
    labelLines: draft.labelLines,
    taskLimit: draft.taskLimit,
    delayInMilliseconds: draft.delayInMilliseconds,
    priorityFactors: draft.priorityFactors,
  }
}

function FactorGroupRow({ label }: { label: string }) {
  return (
    <TableRow sx={{ bgcolor: 'action.hover' }}>
      <TableCell colSpan={2} sx={{ fontWeight: 800 }}>
        {label}
      </TableCell>
    </TableRow>
  )
}

function FactorRow({ label, value, onChange }: {
  label: string
  value: number
  onChange: (value: number) => void
}) {
  return (
    <TableRow>
      <TableCell>{label}</TableCell>
      <TableCell align="right">
        <TextField
          type="number"
          value={value}
          onChange={(event) => onChange(Number(event.target.value))}
          size="small"
          slotProps={{ htmlInput: { 'aria-label': label } }}
          sx={{ width: 120 }}
        />
      </TableCell>
    </TableRow>
  )
}

function mergeRunProgress(
  current: RunProgressState | null,
  event: TaskRunProgressEvent,
  startedAt: number,
  refresh: boolean,
): RunProgressState {
  const status = event.type === 'failed'
    ? 'failed'
    : event.type === 'completed'
      ? 'completed'
      : current?.status ?? 'running'
  const events = [event, ...(current?.events ?? [])].slice(0, progressTimelineLimit)

  return {
    status,
    startedAt: current?.startedAt ?? startedAt,
    refresh: current?.refresh ?? refresh,
    phase: event.phase,
    message: event.error || event.message,
    completedOperations: event.completedOperations,
    totalOperations: event.totalOperations,
    latestEvent: event,
    events,
  }
}

function currentProfile(profiles: ProfileSummary[], id?: string) {
  return profiles.find((profile) => profile.id === id)
}

function unique(values: string[]) {
  return Array.from(new Set(values.filter(Boolean))).sort((left, right) => left.localeCompare(right))
}

function rankedTaskCountLabel(count: number) {
  return `${count} ranked task${count === 1 ? '' : 's'}`
}

function groupCacheOperations(operations: TaskRunCacheOperation[]) {
  return operations.reduce<Record<string, TaskRunCacheOperation[]>>((groups, operation) => {
    groups[operation.source] = groups[operation.source] ?? []
    groups[operation.source].push(operation)
    return groups
  }, {})
}

function cacheStatusLabel(status: TaskRunCache['status']) {
  switch (status) {
    case 'cache':
      return 'served from cache'
    case 'github':
      return 'fetched from GitHub'
    case 'mixed':
      return 'mixed cache and GitHub'
    case 'refreshed':
      return 'refreshed from GitHub'
    case 'disabled':
      return 'disabled'
  }
}

function quotaStatusLabel(status: TaskRunQuota['status']) {
  switch (status) {
    case 'ok':
      return 'healthy'
    case 'low':
      return 'low'
    case 'protected':
      return 'protected'
    case 'exhausted':
      return 'exhausted'
    case 'secondary-limited':
      return 'secondary limited'
    case 'unknown':
      return 'unknown'
  }
}

function quotaSourceLabel(source: TaskRunQuota['source']) {
  switch (source) {
    case 'snapshot':
      return 'saved snapshot'
    case 'headers':
      return 'GitHub headers'
    case 'rate-limit-endpoint':
      return 'rate limit endpoint'
    case 'unavailable':
      return 'unavailable'
  }
}

function sourceLabel(source: string) {
  switch (source) {
    case 'cache':
      return 'cache'
    case 'refresh':
      return 'refresh'
    case 'disabled':
      return 'disabled'
    default:
      return 'GitHub'
  }
}

type ChipColor = 'default' | 'primary' | 'secondary' | 'error' | 'info' | 'success' | 'warning'
type AlertSeverity = 'error' | 'info' | 'success' | 'warning'

function cacheStatusColor(status: TaskRunCache['status']): ChipColor {
  switch (status) {
    case 'cache':
      return 'success'
    case 'mixed':
      return 'warning'
    case 'disabled':
      return 'default'
    default:
      return 'info'
  }
}

function quotaStatusColor(status: TaskRunQuota['status']): ChipColor {
  switch (status) {
    case 'ok':
      return 'success'
    case 'low':
      return 'warning'
    case 'protected':
    case 'exhausted':
    case 'secondary-limited':
      return 'error'
    case 'unknown':
      return 'default'
  }
}

function cacheSourceColor(source: string): ChipColor {
  switch (source) {
    case 'cache':
      return 'success'
    case 'refresh':
      return 'info'
    case 'disabled':
      return 'default'
    default:
      return 'secondary'
  }
}

function cacheAlertSeverity(status: TaskRunCache['status']): AlertSeverity {
  switch (status) {
    case 'cache':
      return 'success'
    case 'mixed':
      return 'warning'
    case 'disabled':
      return 'info'
    default:
      return 'info'
  }
}

function quotaAlertSeverity(status: TaskRunQuota['status']): AlertSeverity {
  switch (status) {
    case 'ok':
      return 'success'
    case 'low':
      return 'warning'
    case 'protected':
    case 'exhausted':
    case 'secondary-limited':
      return 'error'
    case 'unknown':
      return 'info'
  }
}

function shouldConfirmQuotaOverride(quota: TaskRunQuota | null) {
  return quota?.status === 'low'
    || quota?.status === 'protected'
    || quota?.status === 'exhausted'
    || quota?.status === 'secondary-limited'
}

function quotaDetail(quota: TaskRunQuota) {
  if (quota.remaining === undefined)
    return `GitHub quota is ${quotaStatusLabel(quota.status)}. Source: ${quotaSourceLabel(quota.source)}.`

  return `GitHub quota is ${quotaStatusLabel(quota.status)} with ${quota.remaining}/${quota.limit ?? '?'} request(s) remaining. Reserve is ${quota.reserveRequests}. Reset: ${formatQuotaReset(quota)}.`
}

function formatQuotaNumber(remaining?: number, limit?: number) {
  if (remaining === undefined)
    return '-'

  return limit === undefined ? remaining.toString() : `${remaining}/${limit}`
}

function formatQuotaReset(quota: TaskRunQuota) {
  if (quota.resetAt) {
    const resetAt = new Date(quota.resetAt)
    if (!Number.isNaN(resetAt.getTime()))
      return resetAt.toLocaleString()
  }

  if (quota.resetInSeconds !== undefined)
    return `${quota.resetInSeconds}s`

  return '-'
}

function filterTasks(tasks: TaskItem[], filter: {
  repository: string
  status: string
  type: string
  tier: string
  search: string
}) {
  const search = filter.search.trim().toLowerCase()
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
