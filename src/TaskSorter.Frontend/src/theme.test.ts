import { afterEach, describe, expect, it } from 'vitest'
import {
  buildTheme, readStoredThemePreference, resolveThemeMode, themeStorageKey, writeStoredThemePreference,
} from './theme'

describe('theme preferences', () => {
  afterEach(() => localStorage.clear())

  it('defaults to system without a stored preference', () => {
    expect(readStoredThemePreference(localStorage)).toBe('system')
  })

  it('persists valid theme preferences', () => {
    writeStoredThemePreference('dark', localStorage)

    expect(localStorage.getItem(themeStorageKey)).toBe('dark')
    expect(readStoredThemePreference(localStorage)).toBe('dark')
  })

  it('ignores unknown stored preferences', () => {
    localStorage.setItem(themeStorageKey, 'sepia')

    expect(readStoredThemePreference(localStorage)).toBe('system')
  })

  it('resolves system mode from the current color scheme', () => {
    expect(resolveThemeMode('system', true)).toBe('dark')
    expect(resolveThemeMode('system', false)).toBe('light')
    expect(resolveThemeMode('light', true)).toBe('light')
    expect(resolveThemeMode('dark', false)).toBe('dark')
  })

  it('builds light and dark MUI themes', () => {
    expect(buildTheme('light').palette.mode).toBe('light')
    expect(buildTheme('light').palette.text.primary).toBeTruthy()
    expect(buildTheme('dark').palette.mode).toBe('dark')
    expect(buildTheme('dark').palette.text.primary).toBe('#edf3ef')
  })
})
