import type { AuthState } from './types';

// chrome.i18n picks the locale automatically from the browser's UI language,
// falling back to manifest.json's default_locale ("en") — no language picker needed.
const msg = (key: string, substitutions?: string | string[]) =>
  chrome.i18n.getMessage(key, substitutions) || key;

document.documentElement.lang = chrome.i18n.getUILanguage().split('-')[0] ?? 'en';

// Auth view.
const authView = document.getElementById('authView') as HTMLDivElement;

const spinner = '<div class="auth-loading"><span class="spinner"></span></div>';
const checkIcon =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>';

const esc = (s: string) =>
  s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]!));

function renderSignedOut() {
  authView.innerHTML = `
    <p class="auth-title">${msg('authConnectTitle')}</p>
    <p class="auth-sub">${msg('authConnectSubtitle')}</p>
    <button id="signInBtn">${msg('signInButton')}</button>
  `;
  document.getElementById('signInBtn')!.addEventListener('click', doSignIn);
}

function renderSignedIn(state: AuthState) {
  const who = state.user ? `${esc(state.user.username)} · ${esc(state.user.email)}` : msg('signedInFallback');
  authView.innerHTML = `
    <div class="auth-status">
      <span class="dot">${checkIcon}</span>
      <div>
        <div class="auth-title">${msg('authActiveTitle')}</div>
        <div class="auth-sub">${msg('authSyncingSubtitle', who)}</div>
      </div>
    </div>
    <button id="signOutBtn" class="secondary">${msg('signOutButton')}</button>
  `;
  document.getElementById('signOutBtn')!.addEventListener('click', doSignOut);
}

function renderError(errMsg: string) {
  authView.innerHTML = `
    <p class="status error">${esc(errMsg)}</p>
    <button id="retryBtn" class="secondary">${msg('retryButton')}</button>
  `;
  document.getElementById('retryBtn')!.addEventListener('click', loadAuth);
}

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

async function doSignIn() {
  authView.innerHTML = spinner;
  try {
    const res = (await chrome.runtime.sendMessage({ type: 'SIGN_IN' })) as {
      ok: boolean;
      state?: AuthState;
    };
    if (res?.ok && res.state?.authenticated) renderSignedIn(res.state);
    else renderSignedOut();
  } catch {
    renderError(msg('authSignInError'));
  }
}

async function doSignOut() {
  authView.innerHTML = spinner;
  try {
    await chrome.runtime.sendMessage({ type: 'SIGN_OUT' });
  } catch {
    /* ignore */
  }
  renderSignedOut();
}

loadAuth();
