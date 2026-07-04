import { StrictMode, useEffect, useMemo, useState } from 'react'
import { createRoot } from 'react-dom/client'
import CssBaseline from '@mui/material/CssBaseline'
import { ThemeProvider } from '@mui/material/styles'
import { App } from './App'
import {
  buildTheme, readStoredThemePreference, resolveThemeMode, writeStoredThemePreference,
} from './theme'
import type { ThemePreference } from './theme'

function Root() {
  const [themePreference, setThemePreference] = useState<ThemePreference>(() => readStoredThemePreference())
  const systemPrefersDark = useSystemPrefersDark()
  const theme = useMemo(
    () => buildTheme(resolveThemeMode(themePreference, systemPrefersDark)),
    [systemPrefersDark, themePreference])

  function changeThemePreference(preference: ThemePreference) {
    setThemePreference(preference)
    writeStoredThemePreference(preference)
  }

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline enableColorScheme />
      <App themePreference={themePreference} onThemePreferenceChange={changeThemePreference} />
    </ThemeProvider>
  )
}

function useSystemPrefersDark() {
  const [prefersDark, setPrefersDark] = useState(() => readSystemPrefersDark())

  useEffect(() => {
    if (typeof window === 'undefined' || !window.matchMedia)
      return undefined
    const query = window.matchMedia('(prefers-color-scheme: dark)')
    const handleChange = (event: MediaQueryListEvent) => setPrefersDark(event.matches)
    setPrefersDark(query.matches)
    query.addEventListener('change', handleChange)
    return () => query.removeEventListener('change', handleChange)
  }, [])

  return prefersDark
}

function readSystemPrefersDark() {
  return typeof window !== 'undefined'
    && Boolean(window.matchMedia)
    && window.matchMedia('(prefers-color-scheme: dark)').matches
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Root />
  </StrictMode>,
)
