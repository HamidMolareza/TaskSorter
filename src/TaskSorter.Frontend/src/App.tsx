import { Fragment, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import type { DragEndEvent } from '@dnd-kit/core'
import {
  Accordion, AccordionDetails, AccordionSummary, Alert, Avatar, Box, Button, Card, CardContent, Chip, Collapse, Dialog, DialogActions, DialogContent, DialogTitle,
  Divider, IconButton, InputAdornment, LinearProgress, ListItemIcon, Menu, MenuItem, Stack, Tab, Table, TableBody,
  TableCell, TableContainer, TableHead, TableRow, Tabs, TextField, Tooltip, Typography,
} from '@mui/material'
import {
  ArrowDown, ArrowUp, Check, ChevronDown, Database, ExternalLink, FolderGit2, KeyRound, Play, Plus, RefreshCcw,
  GripVertical, Settings2, Tags, Trash2, X,
} from 'lucide-react'
import { DndContext, PointerSensor, closestCenter, useDraggable, useDroppable, useSensor, useSensors } from '@dnd-kit/core'
import { SortableContext, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { api } from './api'
import type {
  LabelDiscovery, ProfileDetail, ProfileDraft, ProfileLabel, ProfileRepository, ProfileSummary, RepositoryTier, TaskItem,
  TaskPriorityFactors, TaskRunCache, TaskRunProgressEvent, TaskRunQuota,
} from './types'

const defaultPriorityFactors: TaskPriorityFactors = { assignmentBonus: 20, lockPenalty: -100 }
const emptyDraft: ProfileDraft = {
  name: '', labelLines: '', taskLimit: 10, delayInMilliseconds: 500,
  priorityFactors: defaultPriorityFactors, repositories: [], labels: [],
}
const tabs = ['Repositories', 'Labels', 'Scoring', 'Ranked Queue', 'Cache', 'Settings']

type ConfirmAction = { title: string; message: string; confirmLabel: string; run: () => Promise<void> } | null

export function App() {
  const [profiles, setProfiles] = useState<ProfileSummary[]>([])
  const [tiers, setTiers] = useState<RepositoryTier[]>([])
  const [selectedProfileId, setSelectedProfileId] = useState('')
  const [draft, setDraft] = useState<ProfileDraft>(emptyDraft)
  const [token, setToken] = useState('')
  const [activeTab, setActiveTab] = useState(0)
  const [status, setStatus] = useState('Loading')
  const [error, setError] = useState('')
  const [isBusy, setIsBusy] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [tierActionId, setTierActionId] = useState('')
  const [profileMenuAnchor, setProfileMenuAnchor] = useState<HTMLElement | null>(null)
  const [createDialogOpen, setCreateDialogOpen] = useState(false)
  const [newProfileName, setNewProfileName] = useState('')
  const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null)
  const [tasks, setTasks] = useState<TaskItem[]>([])
  const [warnings, setWarnings] = useState<string[]>([])
  const [cache, setCache] = useState<TaskRunCache | null>(null)
  const [quota, setQuota] = useState<TaskRunQuota | null>(null)
  const [progress, setProgress] = useState<TaskRunProgressEvent[]>([])
  const [labelDiscovery, setLabelDiscovery] = useState<LabelDiscovery | null>(null)
  const labelDiscoverySignatures = useRef(new Map<string, string>())
  const autosaveTimer = useRef<number | undefined>(undefined)

  useEffect(() => { void loadInitial() }, [])

  useEffect(() => {
    if (!selectedProfileId) return
    let cancelled = false
    void api.getProfile(selectedProfileId)
      .then((profile) => { if (!cancelled) setDraft(profileToDraft(profile)) })
      .catch((reason: Error) => { if (!cancelled) setError(reason.message) })
    return () => { cancelled = true }
  }, [selectedProfileId])

  useEffect(() => {
    if (!draft.id) return
    const timer = window.setTimeout(() => { void persistProfile() }, 750)
    autosaveTimer.current = timer
    return () => {
      window.clearTimeout(timer)
      if (autosaveTimer.current === timer)
        autosaveTimer.current = undefined
    }
  }, [draft.name, draft.labelLines, draft.taskLimit, draft.delayInMilliseconds, draft.priorityFactors, token])

  const repositorySignature = draft.repositories
    .map((repository) => `${repository.id}:${repository.owner}/${repository.name}:${repository.repositoryTierId}:${repository.sortOrder}`)
    .join('|')
  const hasStoredToken = Boolean(profiles.find((profile) => profile.id === draft.id)?.hasGitHubToken)

  useEffect(() => {
    if (!draft.id || !repositorySignature || !hasStoredToken)
      return

    const discoverySignature = `${repositorySignature}:${hasStoredToken}`
    const previousSignature = labelDiscoverySignatures.current.get(draft.id)
    if (previousSignature === discoverySignature)
      return

    if (previousSignature === undefined && activeTab !== 1)
      return

    labelDiscoverySignatures.current.set(draft.id, discoverySignature)

    const timer = window.setTimeout(() => { void discoverLabels() }, 900)
    return () => window.clearTimeout(timer)
  }, [activeTab, draft.id, repositorySignature, hasStoredToken])

  async function loadInitial() {
    setIsBusy(true)
    try {
      const [profileList, tierList] = await Promise.all([api.getProfiles(), api.getRepositoryTiers()])
      setProfiles(profileList)
      setTiers(tierList)
      if (profileList[0]) setSelectedProfileId(profileList[0].id)
      setStatus(`${profileList.length} profile${profileList.length === 1 ? '' : 's'} loaded`)
    } catch (reason) {
      setError(messageOf(reason))
    } finally { setIsBusy(false) }
  }

  async function refreshProfile() {
    if (!draft.id) return
    const profile = await api.getProfile(draft.id)
    setDraft(profileToDraft(profile))
  }

  async function discoverLabels(refresh = false) {
    if (!draft.id)
      return
    try {
      const result = await api.discoverProfileLabels(draft.id, refresh)
      setLabelDiscovery(result)
      setDraft((current) => ({ ...current, labels: result.labels }))
      const changes = [
        result.newLabelCount > 0 ? `${result.newLabelCount} new label${result.newLabelCount === 1 ? '' : 's'} need ranking` : '',
        result.removedLabelCount > 0 ? `${result.removedLabelCount} stale label${result.removedLabelCount === 1 ? '' : 's'} removed` : '',
      ].filter(Boolean).join('; ')
      setStatus(changes || `Labels checked from ${result.cache.status}`)
    } catch (reason) {
      setError(messageOf(reason))
    }
  }

  async function updateLabel(label: ProfileLabel, isIgnored: boolean) {
    if (!draft.id)
      return
    try {
      const saved = await api.updateProfileLabel(draft.id, label.id, { isIgnored })
      setDraft((current) => ({ ...current, labels: sortLabels(current.labels.map((candidate) => candidate.id === saved.id ? saved : candidate)) }))
      setStatus(isIgnored ? 'Label ignored' : 'Label restored; drag it into the ranked list')
    } catch (reason) { setError(messageOf(reason)) }
  }

  async function saveLabelOrder(labelIds: string[]) {
    if (!draft.id)
      return
    try {
      const saved = await api.reorderProfileLabels(draft.id, labelIds)
      setDraft((current) => ({ ...current, labels: sortLabels(saved) }))
      setStatus('Label order saved automatically')
    } catch (reason) { setError(messageOf(reason)) }
  }

  async function refreshProfiles() {
    const profileList = await api.getProfiles()
    setProfiles(profileList)
  }

  async function persistProfile() {
    if (!draft.id || isSaving) return
    setIsSaving(true)
    try {
      await saveProfileNow('Saved automatically')
    } catch (reason) {
      setError(messageOf(reason))
      setStatus('Save failed')
    } finally { setIsSaving(false) }
  }

  async function saveProfileNow(savedStatus: string) {
    if (!draft.id) return
    const saved = await api.updateProfile(draft.id, toSaveRequest(draft, token))
    setDraft(profileToDraft(saved))
    if (token) setToken('')
    await refreshProfiles()
    setStatus(savedStatus)
  }

  async function createProfile() {
    const name = newProfileName.trim()
    if (!name) return
    setIsBusy(true)
    try {
      const saved = await api.createProfile(toSaveRequest({ ...emptyDraft, name }, ''))
      setProfiles((current) => [...current, summaryFromDetail(saved)].sort((a, b) => a.name.localeCompare(b.name)))
      setSelectedProfileId(saved.id)
      setDraft(profileToDraft(saved))
      setNewProfileName('')
      setCreateDialogOpen(false)
      setStatus('Profile created')
    } catch (reason) { setError(messageOf(reason)) } finally { setIsBusy(false) }
  }

  async function deleteProfile() {
    if (!draft.id) return
    await api.deleteProfile(draft.id)
    const remaining = profiles.filter((profile) => profile.id !== draft.id)
    setProfiles(remaining)
    setSelectedProfileId(remaining[0]?.id ?? '')
    setDraft(emptyDraft)
    setTasks([])
    setCache(null)
    setQuota(null)
    setStatus('Profile deleted')
  }

  async function switchProfile(profileId: string) {
    setProfileMenuAnchor(null)
    if (!profileId || profileId === selectedProfileId)
      return

    if (autosaveTimer.current !== undefined) {
      window.clearTimeout(autosaveTimer.current)
      autosaveTimer.current = undefined
    }

    if (!draft.id) {
      setSelectedProfileId(profileId)
      return
    }

    setIsSaving(true)
    try {
      await saveProfileNow('Profile saved before switch')
      setSelectedProfileId(profileId)
      setStatus('Profile switched')
    } catch (reason) {
      setError(messageOf(reason))
      setStatus('Save failed')
    } finally { setIsSaving(false) }
  }

  async function runProfile(refresh = false) {
    if (!draft.id) return
    await saveProfileNow('Profile saved before run')
    setIsBusy(true)
    setError('')
    setWarnings([])
    setProgress([{ type: 'started', phase: 'starting', message: refresh ? 'Refreshing GitHub cache.' : 'Starting profile run.', completedOperations: 0, totalOperations: 0 }])
    try {
      const result = await api.runProfileWithProgress(draft.id, toRunRequest(draft), {
        refresh,
        onEvent: (event) => {
          setProgress((current) => [...current.slice(-11), event])
          if (event.quota) setQuota(event.quota)
        },
      })
      setTasks(result.items)
      setWarnings(result.warnings)
      setCache(result.cache)
      setQuota(result.quota)
      setStatus(`${result.items.length} ranked task${result.items.length === 1 ? '' : 's'}`)
      setActiveTab(3)
    } catch (reason) { setError(messageOf(reason)) } finally { setIsBusy(false) }
  }

  async function addRepository(fullName: string, repositoryTierId: string) {
    if (!draft.id) return
    const parsed = splitRepository(fullName)
    if (!parsed) { setError('Repository must use owner/repository format.'); return }
    try {
      const repository = await api.createProfileRepository(draft.id, { ...parsed, repositoryTierId })
      setDraft((current) => ({ ...current, repositories: [...current.repositories, repository] }))
      setStatus('Repository added')
    } catch (reason) { setError(messageOf(reason)) }
  }

  async function updateRepository(repository: ProfileRepository, owner: string, name: string, repositoryTierId: string) {
    if (!draft.id) return
    try {
      const saved = await api.updateProfileRepository(draft.id, repository.id, { owner, name, repositoryTierId })
      setDraft((current) => ({ ...current, repositories: current.repositories.map((candidate) => candidate.id === saved.id ? saved : candidate) }))
      setStatus('Repository saved automatically')
    } catch (reason) { setError(messageOf(reason)) }
  }

  async function deleteRepository(repository: ProfileRepository) {
    if (!draft.id) return
    await api.deleteProfileRepository(draft.id, repository.id)
    setDraft((current) => ({ ...current, repositories: current.repositories.filter((candidate) => candidate.id !== repository.id) }))
    setStatus('Repository removed')
  }

  async function moveRepository(index: number, direction: -1 | 1) {
    if (!draft.id) return
    const next = [...draft.repositories]
    const target = index + direction
    if (!next[target]) return
    ;[next[index], next[target]] = [next[target], next[index]]
    await reorderRepositories(next)
  }

  async function reorderRepositories(next: ProfileRepository[]) {
    if (!draft.id) return
    setDraft((current) => ({ ...current, repositories: next }))
    try {
      await api.reorderProfileRepositories(draft.id, next.map((repository) => repository.id))
      await refreshProfile()
      setStatus('Repository order saved automatically')
    } catch (reason) { setError(messageOf(reason)); await refreshProfile() }
  }

  async function refreshTiers() { setTiers(await api.getRepositoryTiers()) }
  async function updateTier(tier: RepositoryTier, name: string, score: number) {
    try {
      await api.updateRepositoryTier(tier.id, { name, score })
      await Promise.all([refreshTiers(), refreshProfile()])
      setStatus('Tier saved automatically')
    } catch (reason) { setError(messageOf(reason)) }
  }
  async function createTier(name: string, score: number) {
    await api.createRepositoryTier({ name, score })
    await refreshTiers()
    setStatus('Tier created')
  }
  async function setDefaultTier(tier: RepositoryTier) {
    setTierActionId(tier.id)
    try {
      await api.setDefaultRepositoryTier(tier.id)
      await Promise.all([refreshTiers(), refreshProfile()])
      setStatus('Default tier updated')
    } catch (reason) {
      setError(messageOf(reason))
    } finally {
      setTierActionId('')
    }
  }

  const saveState = useMemo(() => isSaving ? 'Saving...' : draft.id ? 'Autosave enabled' : 'Create a profile to enable autosave', [draft.id, isSaving])
  const defaultTier = tiers.find((tier) => tier.isDefault)
  const activeProfile = profiles.find((profile) => profile.id === selectedProfileId)
  const otherProfiles = profiles.filter((profile) => profile.id !== selectedProfileId)

  return (
    <Box component="main" sx={{ minHeight: '100vh', px: { xs: 2, md: 3 }, py: 3 }}>
      <Stack spacing={2.5} sx={{ maxWidth: 1500, mx: 'auto' }}>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ justifyContent: 'space-between', alignItems: { md: 'center' } }}>
          <Box><Typography variant="overline" color="primary" sx={{ fontWeight: 800, letterSpacing: 0 }}>TaskSorter</Typography><Typography variant="h1">Project queue</Typography></Box>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap', alignItems: 'center' }}><Chip label={status} variant="outlined" /><Chip icon={<Check size={15} />} label={saveState} color={isSaving ? 'warning' : 'success'} variant="outlined" /><Tooltip title="Switch profile"><span><IconButton aria-label="Open profile menu" onClick={(event) => setProfileMenuAnchor(event.currentTarget)} disabled={!activeProfile || isBusy || isSaving}><Avatar sx={{ width: 34, height: 34, bgcolor: 'primary.main', fontSize: 14, fontWeight: 700 }}>{profileInitials(draft.name || activeProfile?.name)}</Avatar></IconButton></span></Tooltip></Stack>
        </Stack>
        {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}
        <Tabs value={activeTab} onChange={(_, value) => setActiveTab(value)} variant="scrollable" scrollButtons="auto" aria-label="TaskSorter workspace tabs">
          {tabs.map((tab) => <Tab key={tab} label={tab} />)}
        </Tabs>
        {activeTab === 0 && <RepositoriesTab repositories={draft.repositories} tiers={tiers} enabled={Boolean(draft.id)} defaultTierId={defaultTier?.id} onAdd={addRepository} onUpdate={updateRepository} onMove={moveRepository} onReorder={reorderRepositories} onDelete={(repository) => setConfirmAction({ title: 'Remove repository?', message: `Remove ${repository.fullName} from this profile?`, confirmLabel: 'Remove repository', run: () => deleteRepository(repository) })} />}
        {activeTab === 1 && <LabelsTab labels={draft.labels} discovery={labelDiscovery} enabled={Boolean(draft.id)} hasToken={hasStoredToken} onDiscover={(refresh) => void discoverLabels(refresh)} onUpdate={updateLabel} onReorder={saveLabelOrder} />}
        {activeTab === 2 && <ScoringTab tiers={tiers} factors={draft.priorityFactors} defaultActionId={tierActionId} onFactors={(priorityFactors) => setDraft((current) => ({ ...current, priorityFactors }))} onCreate={createTier} onUpdate={updateTier} onDefault={setDefaultTier} onDelete={(tier) => setConfirmAction({ title: 'Delete repository tier?', message: tier.assignedRepositoryCount ? `${tier.assignedRepositoryCount} repository assignment(s) use ${tier.name}. They will move to the default tier.` : `Delete ${tier.name}?`, confirmLabel: tier.assignedRepositoryCount ? 'Reassign and delete' : 'Delete tier', run: async () => { await api.deleteRepositoryTier(tier.id, tier.assignedRepositoryCount > 0); await Promise.all([refreshTiers(), refreshProfile()]) } })} />}
        {activeTab === 3 && <QueueTab draft={draft} tasks={tasks} warnings={warnings} progress={progress} cache={cache} quota={quota} busy={isBusy} hasToken={hasStoredToken || Boolean(token)} onUpdate={(patch) => setDraft((current) => ({ ...current, ...patch }))} onRun={() => void runProfile()} onRefresh={() => void runProfile(true)} />}
        {activeTab === 4 && <CacheTab cache={cache} quota={quota} canRun={Boolean(draft.id)} busy={isBusy} onRefresh={() => void runProfile(true)} />}
        {activeTab === 5 && <SettingsTab draft={draft} token={token} hasToken={hasStoredToken} busy={isBusy} onUpdate={(patch) => setDraft((current) => ({ ...current, ...patch }))} onToken={setToken} onDelete={() => setConfirmAction({ title: 'Delete profile?', message: `Delete ${draft.name}? Its repository assignments cannot be recovered.`, confirmLabel: 'Delete profile', run: deleteProfile })} />}
      </Stack>
      <Menu id="profile-menu" anchorEl={profileMenuAnchor} open={Boolean(profileMenuAnchor)} onClose={() => setProfileMenuAnchor(null)}>
        <Box sx={{ px: 2, py: 1.25, minWidth: 240 }}><Typography variant="caption" color="text.secondary">Current profile</Typography><Typography sx={{ fontWeight: 700 }}>{draft.name || activeProfile?.name || 'No profile selected'}</Typography></Box>
        <Divider />
        {otherProfiles.length > 0 ? otherProfiles.map((profile) => <MenuItem key={profile.id} onClick={() => void switchProfile(profile.id)} disabled={isBusy || isSaving}><Avatar sx={{ width: 28, height: 28, mr: 1.25, fontSize: 12 }}>{profileInitials(profile.name)}</Avatar>{profile.name}</MenuItem>) : <MenuItem disabled>No other profiles</MenuItem>}
        <Divider />
        <MenuItem onClick={() => { setProfileMenuAnchor(null); setCreateDialogOpen(true) }} disabled={isBusy}><ListItemIcon><Plus size={17} /></ListItemIcon>Create profile</MenuItem>
      </Menu>
      <Dialog open={createDialogOpen} onClose={() => setCreateDialogOpen(false)} maxWidth="xs" fullWidth><DialogTitle>Create profile</DialogTitle><DialogContent><TextField autoFocus fullWidth label="Profile name" value={newProfileName} onChange={(event) => setNewProfileName(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') void createProfile() }} sx={{ mt: 1 }} /></DialogContent><DialogActions><Button onClick={() => setCreateDialogOpen(false)}>Cancel</Button><Button variant="contained" onClick={() => void createProfile()} disabled={!newProfileName.trim() || isBusy}>Create</Button></DialogActions></Dialog>
      <Dialog open={Boolean(confirmAction)} onClose={() => setConfirmAction(null)} maxWidth="xs" fullWidth><DialogTitle>{confirmAction?.title}</DialogTitle><DialogContent><Typography color="text.secondary">{confirmAction?.message}</Typography></DialogContent><DialogActions><Button onClick={() => setConfirmAction(null)}>Cancel</Button><Button color="error" variant="contained" onClick={() => { const action = confirmAction; setConfirmAction(null); if (action) void action.run().catch((reason) => setError(messageOf(reason))) }}>{confirmAction?.confirmLabel}</Button></DialogActions></Dialog>
    </Box>
  )
}

function SettingsTab(props: { draft: ProfileDraft; token: string; hasToken: boolean; busy: boolean; onUpdate: (patch: Partial<ProfileDraft>) => void; onToken: (value: string) => void; onDelete: () => void }) {
  return <Stack spacing={2}>
    <Card variant="outlined"><CardContent><Stack spacing={2.5}>
      <SectionTitle icon={<Settings2 size={20} />} title="Settings" />
      <Alert severity="info">Changes apply to the active profile and save automatically.</Alert>
      <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' } }}>
        <TextField label="Profile name" value={props.draft.name} disabled={!props.draft.id} onChange={(event) => props.onUpdate({ name: event.target.value })} />
        <TextField label="GitHub token" type="password" value={props.token} disabled={!props.draft.id} placeholder={props.hasToken ? 'Stored token' : 'Required before running'} onChange={(event) => props.onToken(event.target.value)} slotProps={{ input: { startAdornment: <InputAdornment position="start"><KeyRound size={17} /></InputAdornment> } }} />
      </Box>
    </Stack></CardContent></Card>
    <Card variant="outlined" sx={{ borderColor: 'error.light' }}><CardContent><Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}><Box><Typography variant="h2">Delete profile</Typography><Typography variant="body2" color="text.secondary">This removes the active profile and its repository assignments.</Typography></Box><Button color="error" variant="outlined" startIcon={<Trash2 size={17} />} disabled={!props.draft.id || props.busy} onClick={props.onDelete}>Delete profile</Button></Stack></CardContent></Card>
  </Stack>
}

function RepositoriesTab(props: { repositories: ProfileRepository[]; tiers: RepositoryTier[]; enabled: boolean; defaultTierId?: string; onAdd: (fullName: string, tierId: string) => Promise<void>; onUpdate: (repository: ProfileRepository, owner: string, name: string, tierId: string) => Promise<void>; onMove: (index: number, direction: -1 | 1) => Promise<void>; onReorder: (repositories: ProfileRepository[]) => Promise<void>; onDelete: (repository: ProfileRepository) => void }) {
  const [newRepository, setNewRepository] = useState('')
  const [tierId, setTierId] = useState('')
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }))
  useEffect(() => { if (!tierId && props.defaultTierId) setTierId(props.defaultTierId) }, [props.defaultTierId, tierId])
  function handleDragEnd(event: DragEndEvent) {
    if (!event.over || event.active.id === event.over.id)
      return
    const movingId = String(event.active.id)
    const next = props.repositories.filter((repository) => repository.id !== movingId)
    const moving = props.repositories.find((repository) => repository.id === movingId)
    if (!moving)
      return
    const overId = String(event.over.id)
    const targetIndex = overId === 'repository-list-end' ? next.length : next.findIndex((repository) => repository.id === overId)
    if (targetIndex < 0)
      return
    next.splice(targetIndex, 0, moving)
    void props.onReorder(next)
  }

  return <Stack spacing={2}><Card variant="outlined"><CardContent><Stack spacing={2}><SectionTitle icon={<FolderGit2 size={20} />} title="Repositories" /><Stack direction={{ xs: 'column', md: 'row' }} spacing={1}><TextField label="owner/repository" value={newRepository} disabled={!props.enabled} onChange={(event) => setNewRepository(event.target.value)} fullWidth /><TextField select label="Tier" value={tierId} disabled={!props.enabled || !props.tiers.length} onChange={(event) => setTierId(event.target.value)} sx={{ minWidth: 190 }}>{props.tiers.map((tier) => <MenuItem key={tier.id} value={tier.id}>{tier.name} ({tier.score})</MenuItem>)}</TextField><Button variant="contained" startIcon={<Plus size={17} />} disabled={!props.enabled || !newRepository || !tierId} onClick={() => void props.onAdd(newRepository, tierId).then(() => setNewRepository(''))}>Add</Button></Stack><Typography variant="body2" color="text.secondary">Drag rows or use the move controls to rank repositories from top to bottom. Valid changes save automatically.</Typography></Stack></CardContent></Card><DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}><Card variant="outlined"><TableContainer><SortableContext items={props.repositories.map((repository) => repository.id)} strategy={verticalListSortingStrategy}><Table size="small"><TableHead><TableRow><TableCell width={132}>Order</TableCell><TableCell>Repository</TableCell><TableCell>Tier</TableCell><TableCell align="right">Calculated priority</TableCell><TableCell>Validation</TableCell><TableCell width={54} /></TableRow></TableHead><TableBody>{props.repositories.map((repository, index) => <RepositoryRow key={repository.id} repository={repository} tiers={props.tiers} index={index} first={index === 0} last={index === props.repositories.length - 1} onUpdate={props.onUpdate} onMove={props.onMove} onDelete={props.onDelete} />)}{props.repositories.length === 0 ? <TableRow><TableCell colSpan={6}><Typography color="text.secondary">Add at least one repository before running this profile.</Typography></TableCell></TableRow> : <RepositoryDropZone />}</TableBody></Table></SortableContext></TableContainer></Card></DndContext></Stack>
}

function RepositoryRow(props: { repository: ProfileRepository; tiers: RepositoryTier[]; index: number; first: boolean; last: boolean; onUpdate: (repository: ProfileRepository, owner: string, name: string, tierId: string) => Promise<void>; onMove: (index: number, direction: -1 | 1) => Promise<void>; onDelete: (repository: ProfileRepository) => void }) {
  const [owner, setOwner] = useState(props.repository.owner); const [name, setName] = useState(props.repository.name); const [tierId, setTierId] = useState(props.repository.repositoryTierId)
  const sortable = useSortable({ id: props.repository.id })
  const style = { transform: CSS.Transform.toString(sortable.transform), transition: sortable.transition }
  useEffect(() => { setOwner(props.repository.owner); setName(props.repository.name); setTierId(props.repository.repositoryTierId) }, [props.repository])
  useEffect(() => { if (owner === props.repository.owner && name === props.repository.name && tierId === props.repository.repositoryTierId) return; if (!owner.trim() || !name.trim() || !tierId) return; const timer = window.setTimeout(() => { void props.onUpdate(props.repository, owner, name, tierId) }, 650); return () => window.clearTimeout(timer) }, [owner, name, tierId, props])
  return <TableRow ref={sortable.setNodeRef} style={style} sx={{ opacity: sortable.isDragging ? 0.5 : 1 }}><TableCell><Stack direction="row"><Tooltip title="Drag repository"><IconButton aria-label={`Drag ${props.repository.fullName}`} size="small" {...sortable.attributes} {...sortable.listeners}><GripVertical size={17} /></IconButton></Tooltip><Tooltip title="Move up"><span><IconButton aria-label="Move repository up" size="small" disabled={props.first} onClick={() => void props.onMove(props.index, -1)}><ArrowUp size={16} /></IconButton></span></Tooltip><Tooltip title="Move down"><span><IconButton aria-label="Move repository down" size="small" disabled={props.last} onClick={() => void props.onMove(props.index, 1)}><ArrowDown size={16} /></IconButton></span></Tooltip></Stack></TableCell><TableCell><Stack direction="row" spacing={0.5}><TextField aria-label={`Owner for ${props.repository.fullName}`} size="small" value={owner} onChange={(event) => setOwner(event.target.value)} /><Typography sx={{ alignSelf: 'center' }}>/</Typography><TextField aria-label={`Repository for ${props.repository.fullName}`} size="small" value={name} onChange={(event) => setName(event.target.value)} /></Stack></TableCell><TableCell><TextField select aria-label={`Tier for ${props.repository.fullName}`} size="small" value={tierId} onChange={(event) => setTierId(event.target.value)} sx={{ minWidth: 160 }}>{props.tiers.map((tier) => <MenuItem key={tier.id} value={tier.id}>{tier.name} ({tier.score})</MenuItem>)}</TextField></TableCell><TableCell align="right"><Typography sx={{ fontWeight: 700 }}>{props.repository.priorityScore}</Typography><Typography variant="caption" color="text.secondary">tier {props.repository.repositoryTierScore} + order {props.repository.positionScore}</Typography></TableCell><TableCell>{props.repository.validation.length ? props.repository.validation.map((issue) => <Chip key={issue.message} color={issue.severity === 'error' ? 'error' : 'warning'} size="small" label={issue.message} />) : <Chip color="success" size="small" label="Valid" />}</TableCell><TableCell><Tooltip title="Remove repository"><IconButton aria-label="Remove repository" color="error" onClick={() => props.onDelete(props.repository)}><Trash2 size={17} /></IconButton></Tooltip></TableCell></TableRow>
}

function RepositoryDropZone() {
  const droppable = useDroppable({ id: 'repository-list-end' })
  return <TableRow ref={droppable.setNodeRef} sx={{ bgcolor: droppable.isOver ? 'primary.50' : 'transparent' }}><TableCell colSpan={6} sx={{ py: 0.5, borderBottom: 'none' }}><Typography variant="body2" color="text.secondary">Drop here to rank a repository last.</Typography></TableCell></TableRow>
}

function LabelsTab(props: {
  labels: ProfileLabel[]
  discovery: LabelDiscovery | null
  enabled: boolean
  hasToken: boolean
  onDiscover: (refresh: boolean) => void
  onUpdate: (label: ProfileLabel, isIgnored: boolean) => Promise<void>
  onReorder: (labelIds: string[]) => Promise<void>
}) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }))
  const activeLabels = sortLabels(props.labels.filter((label) => !label.isIgnored && !label.isPending))
  const pendingLabels = sortLabels(props.labels.filter((label) => label.isPending))
  const ignoredLabels = sortLabels(props.labels.filter((label) => label.isIgnored))

  function handleDragEnd(event: DragEndEvent) {
    if (!event.over)
      return
    const movingId = String(event.active.id)
    const overId = String(event.over.id)
    if (movingId === overId)
      return
    const moving = activeLabels.find((label) => label.id === movingId) ?? pendingLabels.find((label) => label.id === movingId)
    if (!moving)
      return
    const next = activeLabels.filter((label) => label.id !== movingId)
    const targetIndex = overId === 'label-list-end' ? next.length : next.findIndex((label) => label.id === overId)
    if (targetIndex < 0)
      return
    next.splice(targetIndex, 0, moving)
    void props.onReorder(next.map((label) => label.id))
  }

  function moveLabel(label: ProfileLabel, direction: -1 | 1) {
    const index = activeLabels.findIndex((candidate) => candidate.id === label.id)
    const target = index + direction
    if (index < 0 || !activeLabels[target])
      return
    const next = [...activeLabels]
    ;[next[index], next[target]] = [next[target], next[index]]
    void props.onReorder(next.map((candidate) => candidate.id))
  }

  return <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}><Stack spacing={2}>
    <Card variant="outlined"><CardContent><Stack spacing={2}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
        <SectionTitle icon={<Tags size={20} />} title="Labels" />
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" startIcon={<RefreshCcw size={17} />} disabled={!props.enabled || !props.hasToken} onClick={() => props.onDiscover(false)}>Discover labels</Button>
          <Tooltip title="Bypass cached repository issues"><span><IconButton aria-label="Refresh labels from GitHub" disabled={!props.enabled || !props.hasToken} onClick={() => props.onDiscover(true)}><RefreshCcw size={18} /></IconButton></span></Tooltip>
        </Stack>
      </Stack>
      {!props.hasToken && <Alert severity="info">Save a GitHub token to discover labels from configured repositories.</Alert>}
      {props.discovery && <Alert severity={props.discovery.newLabelCount > 0 || props.discovery.removedLabelCount > 0 ? 'warning' : 'success'}>{[
        props.discovery.newLabelCount > 0 ? `${props.discovery.newLabelCount} new label${props.discovery.newLabelCount === 1 ? '' : 's'} need ranking or must be ignored.` : '',
        props.discovery.removedLabelCount > 0 ? `${props.discovery.removedLabelCount} label${props.discovery.removedLabelCount === 1 ? '' : 's'} no longer exist in configured repositories and were removed.` : '',
      ].filter(Boolean).join(' ') || `Labels checked using ${props.discovery.cache.status}: ${props.discovery.cache.hitCount} cache hit(s), ${props.discovery.cache.gitHubRequestCount} GitHub request(s).`}</Alert>}
      <Typography variant="body2" color="text.secondary">Drag labels to set one unique priority order. Higher rows receive higher label scores.</Typography>
    </Stack></CardContent></Card>

    {pendingLabels.length > 0 && <Card variant="outlined" sx={{ borderColor: 'warning.main' }}><CardContent><Stack spacing={1.5}><Typography variant="h2">New labels</Typography><Typography variant="body2" color="text.secondary">Drag a label into the ranked list to activate it.</Typography><Table size="small"><TableHead><LabelTableHead /></TableHead><TableBody>{pendingLabels.map((label) => <PendingLabelRow key={label.id} label={label} onIgnore={() => void props.onUpdate(label, true)} />)}</TableBody></Table></Stack></CardContent></Card>}

    <Card variant="outlined"><TableContainer><SortableContext items={activeLabels.map((label) => label.id)} strategy={verticalListSortingStrategy}><Table size="small"><TableHead><LabelTableHead /></TableHead><TableBody>{activeLabels.map((label, index) => <SortableLabelRow key={label.id} label={label} first={index === 0} last={index === activeLabels.length - 1} onMove={moveLabel} onIgnore={() => void props.onUpdate(label, true)} />)}<LabelListDropZone empty={activeLabels.length === 0} /></TableBody></Table></SortableContext></TableContainer></Card>

    {ignoredLabels.length > 0 && <Accordion disableGutters><AccordionSummary expandIcon={<ChevronDown size={18} />}><Typography variant="h2">Ignored labels ({ignoredLabels.length})</Typography></AccordionSummary><AccordionDetails><Table size="small"><TableHead><TableRow><TableCell>Label name</TableCell><TableCell width={100}>Action</TableCell></TableRow></TableHead><TableBody>{ignoredLabels.map((label) => <TableRow key={label.id}><TableCell>{label.name}</TableCell><TableCell><Button size="small" onClick={() => void props.onUpdate(label, false)}>Restore</Button></TableCell></TableRow>)}</TableBody></Table></AccordionDetails></Accordion>}
  </Stack></DndContext>
}

function LabelTableHead() {
  return <TableRow><TableCell width={52} /><TableCell>Label name</TableCell><TableCell width={116}>Move</TableCell><TableCell width={56} /></TableRow>
}

function SortableLabelRow(props: { label: ProfileLabel; first: boolean; last: boolean; onMove: (label: ProfileLabel, direction: -1 | 1) => void; onIgnore: () => void }) {
  const sortable = useSortable({ id: props.label.id })
  const style = { transform: CSS.Transform.toString(sortable.transform), transition: sortable.transition }
  return <TableRow ref={sortable.setNodeRef} style={style} sx={{ opacity: sortable.isDragging ? 0.5 : 1 }}><TableCell><Tooltip title="Drag label"><IconButton aria-label={`Drag ${props.label.name}`} size="small" {...sortable.attributes} {...sortable.listeners}><GripVertical size={17} /></IconButton></Tooltip></TableCell><TableCell>{props.label.name}</TableCell><TableCell><Tooltip title="Move label up"><span><IconButton aria-label={`Move ${props.label.name} up`} size="small" disabled={props.first} onClick={() => props.onMove(props.label, -1)}><ArrowUp size={16} /></IconButton></span></Tooltip><Tooltip title="Move label down"><span><IconButton aria-label={`Move ${props.label.name} down`} size="small" disabled={props.last} onClick={() => props.onMove(props.label, 1)}><ArrowDown size={16} /></IconButton></span></Tooltip></TableCell><TableCell><Tooltip title="Ignore label"><IconButton aria-label={`Ignore ${props.label.name}`} onClick={props.onIgnore}><X size={17} /></IconButton></Tooltip></TableCell></TableRow>
}

function PendingLabelRow(props: { label: ProfileLabel; onIgnore: () => void }) {
  const draggable = useDraggable({ id: props.label.id })
  const style = { transform: CSS.Transform.toString(draggable.transform) }
  return <TableRow ref={draggable.setNodeRef} style={style} sx={{ bgcolor: 'warning.50', opacity: draggable.isDragging ? 0.5 : 1 }}><TableCell><Tooltip title="Drag into ranked labels"><IconButton aria-label={`Drag ${props.label.name}`} size="small" {...draggable.attributes} {...draggable.listeners}><GripVertical size={17} /></IconButton></Tooltip></TableCell><TableCell><Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}><Typography>{props.label.name}</Typography><Chip color="warning" size="small" label="New" /></Stack></TableCell><TableCell><Tooltip title="Drag into ranked labels to choose a position"><span><IconButton aria-label={`Move ${props.label.name} up`} size="small" disabled><ArrowUp size={16} /></IconButton></span></Tooltip><Tooltip title="Drag into ranked labels to choose a position"><span><IconButton aria-label={`Move ${props.label.name} down`} size="small" disabled><ArrowDown size={16} /></IconButton></span></Tooltip></TableCell><TableCell><Tooltip title="Ignore label"><IconButton aria-label={`Ignore ${props.label.name}`} onClick={props.onIgnore}><X size={17} /></IconButton></Tooltip></TableCell></TableRow>
}

function LabelListDropZone({ empty }: { empty: boolean }) {
  const droppable = useDroppable({ id: 'label-list-end' })
  return <TableRow ref={droppable.setNodeRef} sx={{ bgcolor: droppable.isOver ? 'primary.50' : 'transparent' }}><TableCell colSpan={4} sx={{ py: empty ? 2 : 0.5, borderBottom: 'none' }}><Typography variant="body2" color="text.secondary">{empty ? 'Drag a new label here to start ranking.' : 'Drop here to rank a label last.'}</Typography></TableCell></TableRow>
}

function ScoringTab(props: { tiers: RepositoryTier[]; factors: TaskPriorityFactors; defaultActionId: string; onFactors: (value: TaskPriorityFactors) => void; onCreate: (name: string, score: number) => Promise<void>; onUpdate: (tier: RepositoryTier, name: string, score: number) => Promise<void>; onDefault: (tier: RepositoryTier) => Promise<void>; onDelete: (tier: RepositoryTier) => void }) {
  const [newName, setNewName] = useState(''); const [newScore, setNewScore] = useState(0)
  return <Stack spacing={2}><Card variant="outlined"><CardContent><Stack spacing={2}><SectionTitle icon={<Settings2 size={20} />} title="Repository tiers" /><Stack direction={{ xs: 'column', md: 'row' }} spacing={1}><TextField label="Tier name" value={newName} onChange={(event) => setNewName(event.target.value)} /><TextField label="Score" type="number" value={newScore} onChange={(event) => setNewScore(Number(event.target.value))} /><Button variant="contained" startIcon={<Plus size={17} />} disabled={!newName.trim()} onClick={() => void props.onCreate(newName, newScore).then(() => { setNewName(''); setNewScore(0) })}>Add tier</Button></Stack><TableContainer><Table size="small"><TableHead><TableRow><TableCell>Tier</TableCell><TableCell>Score</TableCell><TableCell>Default</TableCell><TableCell>Assignments</TableCell><TableCell width={124} /></TableRow></TableHead><TableBody>{props.tiers.map((tier) => <TierRow key={tier.id} tier={tier} defaultActionId={props.defaultActionId} onUpdate={props.onUpdate} onDefault={props.onDefault} onDelete={props.onDelete} />)}</TableBody></Table></TableContainer></Stack></CardContent></Card><Card variant="outlined"><CardContent><Stack spacing={2}><SectionTitle icon={<Settings2 size={20} />} title="Tuning" /><Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}><TextField label="Assignment bonus" type="number" value={props.factors.assignmentBonus} onChange={(event) => props.onFactors({ ...props.factors, assignmentBonus: Number(event.target.value) })} /><TextField label="Lock penalty" type="number" value={props.factors.lockPenalty} onChange={(event) => props.onFactors({ ...props.factors, lockPenalty: Number(event.target.value) })} /></Box></Stack></CardContent></Card></Stack>
}

function TierRow(props: { tier: RepositoryTier; defaultActionId: string; onUpdate: (tier: RepositoryTier, name: string, score: number) => Promise<void>; onDefault: (tier: RepositoryTier) => Promise<void>; onDelete: (tier: RepositoryTier) => void }) { const [name, setName] = useState(props.tier.name); const [score, setScore] = useState(props.tier.score); useEffect(() => { setName(props.tier.name); setScore(props.tier.score) }, [props.tier]); useEffect(() => { if (name === props.tier.name && score === props.tier.score) return; if (!name.trim()) return; const timer = window.setTimeout(() => { void props.onUpdate(props.tier, name, score) }, 650); return () => window.clearTimeout(timer) }, [name, score, props]); return <TableRow><TableCell><TextField aria-label={`Tier name ${props.tier.name}`} size="small" value={name} onChange={(event) => setName(event.target.value)} /></TableCell><TableCell><TextField aria-label={`Tier score ${props.tier.name}`} size="small" type="number" value={score} onChange={(event) => setScore(Number(event.target.value))} /></TableCell><TableCell>{props.tier.isDefault ? <Chip size="small" color="primary" label="Default" /> : <Button size="small" disabled={Boolean(props.defaultActionId)} onClick={() => void props.onDefault(props.tier)}>{props.defaultActionId === props.tier.id ? 'Setting...' : 'Set default'}</Button>}</TableCell><TableCell>{props.tier.assignedRepositoryCount}</TableCell><TableCell><Tooltip title={props.tier.isDefault ? 'Choose another default before deleting' : 'Delete tier'}><span><IconButton color="error" disabled={props.tier.isDefault} onClick={() => props.onDelete(props.tier)}><Trash2 size={17} /></IconButton></span></Tooltip></TableCell></TableRow> }

function QueueTab(props: { draft: ProfileDraft; tasks: TaskItem[]; warnings: string[]; progress: TaskRunProgressEvent[]; cache: TaskRunCache | null; quota: TaskRunQuota | null; busy: boolean; hasToken: boolean; onUpdate: (patch: Partial<ProfileDraft>) => void; onRun: () => void; onRefresh: () => void }) {
  const [expandedTaskKey, setExpandedTaskKey] = useState('')
  const latestProgress = props.progress[props.progress.length - 1]
  const progressPercent = latestProgress && latestProgress.totalOperations > 0
    ? Math.min(100, Math.round((latestProgress.completedOperations / latestProgress.totalOperations) * 100))
    : undefined
  const emptyMessage = queueEmptyMessage(props.draft, props.hasToken)

  return <Stack spacing={2}>
    <Card variant="outlined"><CardContent><Stack spacing={2}>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ justifyContent: 'space-between', alignItems: { md: 'center' } }}>
        <SectionTitle icon={<Play size={20} />} title="Ranked Queue" />
        <Stack direction="row" spacing={1}>
          <Button variant="contained" startIcon={<Play size={17} />} disabled={!props.draft.id || props.busy} onClick={props.onRun}>Run</Button>
          <Tooltip title="Force GitHub refresh"><span><IconButton aria-label="Force GitHub refresh" disabled={!props.draft.id || props.busy} onClick={props.onRefresh}><RefreshCcw size={18} /></IconButton></span></Tooltip>
        </Stack>
      </Stack>
      <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
        <TextField label="Top" type="number" value={props.draft.taskLimit} disabled={!props.draft.id || props.busy} slotProps={{ htmlInput: { min: 1 } }} onChange={(event) => props.onUpdate({ taskLimit: Number(event.target.value) })} />
        <TextField label="Delay (ms)" type="number" value={props.draft.delayInMilliseconds} disabled={!props.draft.id || props.busy} slotProps={{ htmlInput: { min: 0 } }} onChange={(event) => props.onUpdate({ delayInMilliseconds: Number(event.target.value) })} />
      </Box>
      <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap' }}>
        <Chip size="small" label={`${props.tasks.length} ranked`} />
        {props.cache && <Chip size="small" color={cacheColor(props.cache.status)} label={`Cache: ${props.cache.status}`} />}
        {props.cache && <Chip size="small" label={`${props.cache.gitHubRequestCount} GitHub request${props.cache.gitHubRequestCount === 1 ? '' : 's'}`} />}
        {props.quota && <Chip size="small" color={quotaColor(props.quota.status)} label={quotaSummary(props.quota)} />}
        {props.warnings.length > 0 && <Chip size="small" color="warning" label={`${props.warnings.length} warning${props.warnings.length === 1 ? '' : 's'}`} />}
      </Stack>
      {props.busy && (progressPercent === undefined ? <LinearProgress /> : <LinearProgress variant="determinate" value={progressPercent} />)}
      {latestProgress && <Stack spacing={1}>
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap', alignItems: 'center' }}>
          <Chip size="small" color={progressColor(latestProgress.type)} label={latestProgress.phase} />
          {latestProgress.source && <Chip size="small" color={sourceColor(latestProgress.source)} label={latestProgress.source} />}
          {latestProgress.operation && <Chip size="small" variant="outlined" label={latestProgress.operation} />}
          {latestProgress.target && <Typography variant="body2" color="text.secondary">{latestProgress.target}</Typography>}
        </Stack>
        <Typography variant="body2">{latestProgress.message}</Typography>
      </Stack>}
    </Stack></CardContent></Card>

    {props.progress.length > 1 && <Card variant="outlined"><CardContent><Stack spacing={1}>{props.progress.slice(-5).map((event, index) => <Stack key={`${event.phase}-${index}`} direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { sm: 'center' } }}><Chip size="small" label={event.phase} /><Typography variant="body2" color="text.secondary">{event.message}</Typography></Stack>)}</Stack></CardContent></Card>}
    {props.warnings.map((warning) => <Alert key={warning} severity="warning">{warning}</Alert>)}

    <Card variant="outlined"><TableContainer><Table size="small"><TableHead><TableRow><TableCell width={44} /><TableCell width={64}>Rank</TableCell><TableCell>Task</TableCell><TableCell>Repository / tier</TableCell><TableCell>Signals</TableCell><TableCell align="right">Score</TableCell></TableRow></TableHead><TableBody>
      {props.tasks.map((task) => {
        const key = `${task.repository}-${task.id}`
        const expanded = expandedTaskKey === key
        return <Fragment key={key}>
          <TableRow hover>
            <TableCell><IconButton aria-label={`${expanded ? 'Hide' : 'Show'} details for ${task.title}`} size="small" onClick={() => setExpandedTaskKey(expanded ? '' : key)}><ChevronDown size={17} style={{ transform: expanded ? 'rotate(180deg)' : 'rotate(0deg)', transition: 'transform 120ms ease' }} /></IconButton></TableCell>
            <TableCell><Chip size="small" color={task.rank <= 3 ? 'primary' : 'default'} label={`#${task.rank}`} /></TableCell>
            <TableCell sx={{ minWidth: 280 }}><Stack spacing={0.75}><Typography component="a" href={task.url} target="_blank" rel="noreferrer" color="inherit" sx={{ fontWeight: 700, textDecoration: 'none', '&:hover': { textDecoration: 'underline' } }}>{task.title}</Typography><Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: 'wrap' }}><Chip size="small" variant="outlined" label={task.type} /><Chip size="small" variant="outlined" label={`#${task.id}`} />{task.labels.slice(0, 4).map((label) => <Chip key={label} size="small" label={label} />)}{task.labels.length > 4 && <Chip size="small" label={`+${task.labels.length - 4}`} />}</Stack></Stack></TableCell>
            <TableCell><Stack spacing={0.75}><Typography variant="body2" sx={{ fontWeight: 700 }}>{task.repository}</Typography><Chip size="small" color="secondary" variant="outlined" label={task.projectTier} sx={{ alignSelf: 'flex-start' }} /></Stack></TableCell>
            <TableCell><Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: 'wrap' }}><Chip size="small" label={task.status || 'status/default'} /><Chip size="small" label={task.size || 'size/default'} /><Chip size="small" color={task.assigned ? 'success' : 'default'} label={task.assigned ? 'Assigned' : 'Unassigned'} />{task.locked && <Chip size="small" color="warning" label="Locked" />}</Stack></TableCell>
            <TableCell align="right"><Stack spacing={0.5} sx={{ alignItems: 'flex-end' }}><Typography variant="h2" sx={{ color: scoreColor(task.score) }}>{formatScore(task.score)}</Typography><Typography variant="caption" color="text.secondary">R {task.scoreBreakdown.repository} / L {task.scoreBreakdown.labels} / A {task.scoreBreakdown.assignment} / K {task.scoreBreakdown.lock}</Typography></Stack></TableCell>
          </TableRow>
          <TableRow>
            <TableCell colSpan={6} sx={{ p: 0, borderBottom: expanded ? undefined : 'none', bgcolor: 'action.hover' }}>
              <Collapse in={expanded} timeout="auto" unmountOnExit>
                <Box sx={{ p: 2 }}>
                  <Stack spacing={1.5}>
                    <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap', alignItems: 'center' }}>
                      <Typography variant="body2" sx={{ fontWeight: 700 }}>Score breakdown</Typography>
                      <Chip size="small" label={`Repository ${task.scoreBreakdown.repository}`} />
                      <Chip size="small" label={`Labels ${task.scoreBreakdown.labels}`} />
                      <Chip size="small" label={`Assignment ${task.scoreBreakdown.assignment}`} />
                      <Chip size="small" label={`Lock ${task.scoreBreakdown.lock}`} />
                    </Stack>
                    <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap' }}>
                      <Chip size="small" variant="outlined" label={`Created ${formatDate(task.createdAt)}`} />
                      <Chip size="small" variant="outlined" label={`Updated ${formatDate(task.updatedAt)}`} />
                      {task.unscoredLabels.length > 0 ? task.unscoredLabels.map((label) => <Chip key={label} size="small" color="warning" variant="outlined" label={`Unscored ${label}`} />) : <Chip size="small" variant="outlined" label="No unscored labels" />}
                    </Stack>
                    <Button href={task.url} target="_blank" rel="noreferrer" size="small" variant="outlined" startIcon={<ExternalLink size={16} />} sx={{ alignSelf: 'flex-start' }}>Open task</Button>
                  </Stack>
                </Box>
              </Collapse>
            </TableCell>
          </TableRow>
        </Fragment>
      })}
      {props.tasks.length === 0 && <TableRow><TableCell colSpan={6}><Box sx={{ py: 4, textAlign: 'center' }}><Typography color="text.secondary">{emptyMessage}</Typography></Box></TableCell></TableRow>}
    </TableBody></Table></TableContainer></Card>
  </Stack>
}

function queueEmptyMessage(draft: ProfileDraft, hasToken: boolean) {
  if (!draft.id) return 'Create or select a profile before running the queue.'
  if (draft.repositories.length === 0) return 'Add at least one repository before running the queue.'
  if (!hasToken) return 'Add a GitHub token in Settings before running the queue.'
  return 'Run this profile to see ranked tasks.'
}

function cacheColor(status: TaskRunCache['status']) {
  if (status === 'cache') return 'success' as const
  if (status === 'github' || status === 'refreshed') return 'warning' as const
  if (status === 'mixed') return 'info' as const
  return 'default' as const
}

function quotaColor(status: TaskRunQuota['status']) {
  if (status === 'ok') return 'success' as const
  if (status === 'low' || status === 'protected') return 'warning' as const
  if (status === 'exhausted' || status === 'secondary-limited') return 'error' as const
  return 'default' as const
}

function progressColor(type: TaskRunProgressEvent['type']) {
  if (type === 'completed') return 'success' as const
  if (type === 'failed') return 'error' as const
  if (type === 'started') return 'primary' as const
  return 'info' as const
}

function sourceColor(source: NonNullable<TaskRunProgressEvent['source']>) {
  if (source === 'cache') return 'success' as const
  if (source === 'github' || source === 'refresh') return 'warning' as const
  return 'default' as const
}

function quotaSummary(quota: TaskRunQuota) {
  return quota.remaining === undefined
    ? `Quota: ${quota.status}`
    : `Quota: ${quota.remaining}/${quota.limit ?? '?'}`
}

function scoreColor(score?: number) {
  if (score === undefined) return 'text.secondary'
  if (score < 0) return 'error.main'
  if (score < 100) return 'warning.main'
  return 'success.main'
}

function formatScore(score?: number) {
  return score === undefined ? 'n/a' : score.toLocaleString()
}

function formatDate(value?: string) {
  if (!value) return 'n/a'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function CacheTab({ cache, quota, canRun, busy, onRefresh }: { cache: TaskRunCache | null; quota: TaskRunQuota | null; canRun: boolean; busy: boolean; onRefresh: () => void }) { return <Card variant="outlined"><CardContent><Stack spacing={2}><Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}><SectionTitle icon={<Database size={20} />} title="Cache" /><Button variant="outlined" color="warning" startIcon={<RefreshCcw size={17} />} disabled={!canRun || busy} onClick={onRefresh}>Clear cache and refresh</Button></Stack>{cache ? <><Alert severity={cache.status === 'cache' ? 'success' : 'info'}>Last run: {cache.status}. {cache.hitCount} cache hit(s), {cache.gitHubRequestCount} GitHub request(s), TTL {cache.durationSeconds}s.</Alert><Table size="small"><TableHead><TableRow><TableCell>Operation</TableCell><TableCell>Target</TableCell><TableCell>Source</TableCell></TableRow></TableHead><TableBody>{cache.operations.map((operation) => <TableRow key={`${operation.operation}-${operation.target}`}><TableCell>{operation.operation}</TableCell><TableCell>{operation.target}</TableCell><TableCell><Chip size="small" label={operation.source} /></TableCell></TableRow>)}</TableBody></Table></> : <Typography color="text.secondary">No run data yet.</Typography>}{quota && <Alert severity={quota.status === 'ok' ? 'success' : quota.status === 'low' ? 'warning' : 'info'}>Quota: {quota.status}{quota.remaining === undefined ? '' : `, ${quota.remaining}/${quota.limit ?? '?'} remaining`}. Source: {quota.source}.</Alert>}</Stack></CardContent></Card> }

function SectionTitle({ icon, title }: { icon: ReactNode; title: string }) { return <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>{icon}<Typography variant="h2">{title}</Typography></Stack> }
function profileToDraft(profile: ProfileDetail): ProfileDraft { return { id: profile.id, name: profile.name, labelLines: profile.labelLines, taskLimit: profile.taskLimit, delayInMilliseconds: profile.delayInMilliseconds, priorityFactors: profile.priorityFactors, repositories: profile.repositories, labels: profile.labels } }
function summaryFromDetail(profile: ProfileDetail): ProfileSummary { const { labelLines: _, priorityFactors: __, repositories: ___, labels: ____, createdAt: _____, ...summary } = profile; return summary }
function profileInitials(name?: string) { return name?.trim().split(/\s+/).map((part) => part[0]).join('').slice(0, 2).toUpperCase() || '?' }
function toSaveRequest(draft: ProfileDraft, token: string) { return { name: draft.name, labelLines: draft.labelLines, taskLimit: draft.taskLimit, delayInMilliseconds: draft.delayInMilliseconds, priorityFactors: draft.priorityFactors, ...(token ? { gitHubToken: token } : {}) } }
function toRunRequest(draft: ProfileDraft) { return { labelLines: draft.labelLines, taskLimit: draft.taskLimit, delayInMilliseconds: draft.delayInMilliseconds, priorityFactors: draft.priorityFactors } }
function splitRepository(value: string) { const [owner, name, ...rest] = value.trim().split('/'); return owner && name && rest.length === 0 ? { owner, name } : null }
function messageOf(reason: unknown) { return reason instanceof Error ? reason.message : 'Request failed.' }

function sortLabels(labels: ProfileLabel[]) {
  return labels
    .map((label, index) => ({ label, index }))
    .sort((left, right) => Number(left.label.isIgnored) - Number(right.label.isIgnored)
      || Number(left.label.isPending) - Number(right.label.isPending)
      || left.index - right.index)
    .map(({ label }) => label)
}
