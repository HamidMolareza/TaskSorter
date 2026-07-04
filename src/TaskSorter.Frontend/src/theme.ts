import { createTheme } from '@mui/material/styles'
import type { PaletteMode } from '@mui/material'

export type ThemePreference = 'light' | 'dark' | 'system'

export const themeStorageKey = 'tasksorter.theme'

export function readStoredThemePreference(storage: Storage | undefined = safeStorage()): ThemePreference {
  const value = readStorageValue(storage)
  return isThemePreference(value) ? value : 'system'
}

export function writeStoredThemePreference(preference: ThemePreference, storage: Storage | undefined = safeStorage()) {
  try {
    storage?.setItem(themeStorageKey, preference)
  } catch {
    // Storage can be blocked in private browsing or hardened browser contexts.
  }
}

export function resolveThemeMode(preference: ThemePreference, systemPrefersDark: boolean): PaletteMode {
  return preference === 'system'
    ? systemPrefersDark ? 'dark' : 'light'
    : preference
}

export function buildTheme(mode: PaletteMode) {
  return createTheme({
    palette: {
      mode,
      primary: {
        main: mode === 'dark' ? '#5fb89d' : '#1f6f5b',
      },
      secondary: {
        main: mode === 'dark' ? '#8bbcf0' : '#2563a8',
      },
      warning: {
        main: mode === 'dark' ? '#e0a15a' : '#b66b1f',
      },
      background: {
        default: mode === 'dark' ? '#101412' : '#f6f7f4',
        paper: mode === 'dark' ? '#171c19' : '#ffffff',
      },
      ...(mode === 'dark'
        ? {
            text: {
              primary: '#edf3ef',
              secondary: '#aeb9b2',
            },
          }
        : {}),
    },
    shape: {
      borderRadius: 8,
    },
    typography: {
      fontFamily: '"IBM Plex Sans", Aptos, "Noto Sans", sans-serif',
      h1: {
        fontSize: '2rem',
        fontWeight: 700,
        letterSpacing: 0,
      },
      h2: {
        fontSize: '1.1rem',
        fontWeight: 700,
        letterSpacing: 0,
      },
      button: {
        letterSpacing: 0,
        textTransform: 'none',
      },
    },
    components: {
      MuiButton: {
        defaultProps: {
          disableElevation: true,
        },
      },
    },
  })
}

function isThemePreference(value: string | null): value is ThemePreference {
  return value === 'light' || value === 'dark' || value === 'system'
}

function readStorageValue(storage: Storage | undefined) {
  try {
    return storage?.getItem(themeStorageKey) ?? null
  } catch {
    return null
  }
}

function safeStorage() {
  return typeof window === 'undefined' ? undefined : window.localStorage
}
