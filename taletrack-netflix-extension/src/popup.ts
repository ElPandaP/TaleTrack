/**
 * Script of the extension's popup (`popup.html`).
 *
 * Shows whether the extension is connected to a TaleTrack account and lets the user sign in
 * (which opens the web app's authorization page) or sign out. Every action goes through the
 * background service worker. It also has a light/dark theme toggle. Texts come from the
 * `_locales` message bundles through `chrome.i18n`.
 *
 * @module
 */

import type { AuthState } from './types';

/**
 * A localized text from the `_locales` bundles, or the key itself when it is missing.
 * `chrome.i18n` picks the locale from the browser's UI language, falling back to
 * manifest.json's `default_locale` ("en").
 */
const msg = (key: string, substitutions?: string | string[]) =>
  chrome.i18n.getMessage(key, substitutions) || key;

document.documentElement.lang = chrome.i18n.getUILanguage().split('-')[0] ?? 'en';

/**
 * localStorage key of the chosen theme. The theme is light by default like the web app, dark when
 * chosen with the toggle, and kept in the popup's own localStorage (synchronous, so the first
 * paint already has the right theme).
 */
const THEME_KEY = 'tt-theme';
/** Toggle icons (Lucide, the icon set the web app uses): sun for light, moon for dark. */
const sunIcon =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/></svg>';
const moonIcon =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/></svg>';
/** The theme toggle button in the popup's header. */
const themeToggle = document.getElementById('themeToggle') as HTMLButtonElement;

/** The stored theme, light when none is stored or storage is unavailable. */
function readTheme(): 'light' | 'dark' {
  try {
    return localStorage.getItem(THEME_KEY) === 'dark' ? 'dark' : 'light';
  } catch {
    return 'light';
  }
}

/** The theme currently shown. */
let theme = readTheme();

/** Applies the current theme to the page and shows its icon on the toggle. */
function applyTheme() {
  document.documentElement.dataset.theme = theme;
  themeToggle.innerHTML = theme === 'dark' ? moonIcon : sunIcon;
}

themeToggle.setAttribute('aria-label', msg('toggleThemeAria'));
themeToggle.title = msg('toggleThemeAria');
themeToggle.addEventListener('click', () => {
  theme = theme === 'dark' ? 'light' : 'dark';
  try {
    localStorage.setItem(THEME_KEY, theme);
  } catch {
    /* storage unavailable: the choice just lasts while the popup is open */
  }
  applyTheme();
});
applyTheme();

/** Container the session views are rendered into. */
const authView = document.getElementById('authView') as HTMLDivElement;

/** Loading placeholder shown while waiting for the service worker. */
const spinner = '<div class="loading"><span class="spinner"></span></div>';
/** Lucide icons, the same set the web app uses: a check mark and a right arrow. */
const checkIcon =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20 6 9 17l-5-5"/></svg>';
const arrowIcon =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14"/><path d="m12 5 7 7-7 7"/></svg>';

/** Escapes text for safe insertion into HTML. */
const esc = (s: string) =>
  s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]!));

/** Shows the signed-out view with the sign-in button. */
function renderSignedOut() {
  authView.innerHTML = `
    <h1 class="title">${msg('authConnectTitle')}</h1>
    <p class="sub">${msg('authConnectSubtitle')}</p>
    <button id="signInBtn" class="btn btn-primary">${msg('signInButton')} ${arrowIcon}</button>
  `;
  document.getElementById('signInBtn')!.addEventListener('click', doSignIn);
}

/** Shows the signed-in view, with the account's username and email when known, and the sign-out button. */
function renderSignedIn(state: AuthState) {
  const account = state.user
    ? `<div class="account">
        <p class="account-label">${msg('accountLabel')}</p>
        <p class="account-value">${esc(state.user.username)} · ${esc(state.user.email)}</p>
      </div>`
    : '';
  authView.innerHTML = `
    <div class="heading-row">
      <span class="check">${checkIcon}</span>
      <h1 class="title">${msg('authActiveTitle')}</h1>
    </div>
    <p class="sub">${msg('authSyncingSubtitle')}</p>
    ${account}
    <button id="signOutBtn" class="btn btn-outline">${msg('signOutButton')}</button>
  `;
  document.getElementById('signOutBtn')!.addEventListener('click', doSignOut);
}

/** Shows an error message with a retry button. */
function renderError(errMsg: string) {
  authView.innerHTML = `
    <p class="error">${esc(errMsg)}</p>
    <button id="retryBtn" class="btn btn-outline">${msg('retryButton')}</button>
  `;
  document.getElementById('retryBtn')!.addEventListener('click', loadAuth);
}

/** Asks the background service worker for the session state and renders the matching view. */
async function loadAuth() {
  authView.innerHTML = spinner;
  try {
    const state = (await chrome.runtime.sendMessage({ type: 'AUTH_STATE' })) as AuthState;
    if (state?.authenticated) renderSignedIn(state);
    else renderSignedOut();
  } catch {
    renderError(msg('authContactError'));
  }
}

/**
 * Asks the background service worker to open the web app's authorization tab, then closes the
 * popup. Reopening it after authorizing shows the signed-in state.
 */
async function doSignIn() {
  try {
    await chrome.runtime.sendMessage({ type: 'SIGN_IN' });
    window.close();
  } catch {
    renderError(msg('authSignInError'));
  }
}

/** Signs out through the background service worker and shows the signed-out view. */
async function doSignOut() {
  authView.innerHTML = spinner;
  try {
    await chrome.runtime.sendMessage({ type: 'SIGN_OUT' });
  } catch {
    /* shown as signed out anyway; reopening the popup reflects the real state */
  }
  renderSignedOut();
}

loadAuth();
