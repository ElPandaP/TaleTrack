import type { ExtractDataMessage, ExtractDataResponse, AuthState, NetflixMedia } from './types';

// Auth view.
const authView = document.getElementById('authView') as HTMLDivElement;

const spinner = '<div class="auth-loading"><span class="spinner"></span></div>';
const checkIcon =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>';

const esc = (s: string) =>
  s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]!));

function renderSignedOut() {
  authView.innerHTML = `
    <p class="auth-title">Conecta tu cuenta de TaleTrack</p>
    <p class="auth-sub">Inicia sesión para que la extensión registre automáticamente lo que ves en Netflix.</p>
    <button id="signInBtn">Iniciar sesión</button>
  `;
  document.getElementById('signInBtn')!.addEventListener('click', doSignIn);
}

function renderSignedIn(state: AuthState) {
  const who = state.user ? `${esc(state.user.username)} · ${esc(state.user.email)}` : 'Sesión iniciada';
  authView.innerHTML = `
    <div class="auth-status">
      <span class="dot">${checkIcon}</span>
      <div>
        <div class="auth-title">TaleTrack activo</div>
        <div class="auth-sub">Sincronizando tu Netflix — ${who}</div>
      </div>
    </div>
    <button id="signOutBtn" class="secondary">Cerrar sesión</button>
  `;
  document.getElementById('signOutBtn')!.addEventListener('click', doSignOut);
}

function renderError(msg: string) {
  authView.innerHTML = `
    <p class="status error">${esc(msg)}</p>
    <button id="retryBtn" class="secondary">Reintentar</button>
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
    renderError('No se pudo contactar con la extensión. Recarga la página.');
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
    renderError('No se pudo completar el inicio de sesión.');
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

// Manual extraction (debug view, behind a <details>).
const extractBtn = document.getElementById('extractBtn') as HTMLButtonElement;
const resultDiv = document.getElementById('result') as HTMLDivElement;

const statusRow = (kind: 'loading' | 'success' | 'error', text: string): string => {
  const icon = kind === 'loading' ? '<span class="spinner"></span>' : '';
  return `<p class="status ${kind}">${icon}${text}</p>`;
};

extractBtn.addEventListener('click', async () => {
  resultDiv.innerHTML = statusRow('loading', 'Extrayendo datos…');
  extractBtn.disabled = true;

  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });

    if (!tab) {
      throw new Error('No se pudo obtener la pestaña activa');
    }

    if (!tab.id) {
      throw new Error('La pestaña no tiene ID');
    }

    if (!tab.url) {
      throw new Error('La pestaña no tiene URL');
    }

    if (!tab.url.includes('netflix.com')) {
      resultDiv.innerHTML = statusRow('error', 'Abre una página de Netflix primero.');
      extractBtn.disabled = false;
      return;
    }

    const message: ExtractDataMessage = { action: 'extractData' };

    chrome.tabs.sendMessage(
      tab.id,
      message,
      (response: ExtractDataResponse) => {
        if (chrome.runtime.lastError) {
          resultDiv.innerHTML = statusRow('error', `Error: ${chrome.runtime.lastError.message}`);
          extractBtn.disabled = false;
          return;
        }

        if (response.success && response.data) {
          displayData(response.data);
        } else {
          resultDiv.innerHTML = statusRow('error', response.error || 'Error desconocido');
        }

        extractBtn.disabled = false;
      }
    );

  } catch (error) {
    resultDiv.innerHTML = statusRow('error', `Error: ${(error as Error).message}`);
    extractBtn.disabled = false;
  }
});

/** "1h 47min" / "48min" from seconds. */
function fmtSecs(totalSeconds: number): string {
  const s = Math.max(0, Math.round(totalSeconds));
  const h = Math.floor(s / 3600);
  const m = Math.round((s % 3600) / 60);
  return h > 0 ? `${h}h ${m}min` : `${m}min`;
}

function displayData(data: NetflixMedia) {
  const isSeries = data.type === 'series';

  const progressValue =
    data.progressPercent !== undefined
      ? `${data.progressPercent}%` +
        (data.positionSeconds !== undefined && data.runtimeSeconds !== undefined
          ? ` · ${fmtSecs(data.positionSeconds)} / ${fmtSecs(data.runtimeSeconds)}`
          : '')
      : null;

  const totalDuration =
    data.runtimeSeconds !== undefined ? fmtSecs(data.runtimeSeconds) : data.duration || null;

  resultDiv.innerHTML = `
    ${statusRow('success', 'Datos extraídos correctamente')}
    <div class="media-info">
      <div class="info-row">
        <span class="info-label">Título</span>
        <span class="info-value">${data.title}</span>
      </div>
      ${data.year ? `
        <div class="info-row">
          <span class="info-label">Año</span>
          <span class="info-value">${data.year}</span>
        </div>
      ` : ''}
      <div class="info-row">
        <span class="info-label">Tipo</span>
        <span class="info-value">${isSeries ? 'Serie' : 'Película'}</span>
      </div>
      ${isSeries && data.season ? `
        <div class="info-row">
          <span class="info-label">Temporada</span>
          <span class="info-value">${data.season}</span>
        </div>
      ` : ''}
      ${isSeries && data.episode ? `
        <div class="info-row">
          <span class="info-label">Episodio</span>
          <span class="info-value">${data.episode}</span>
        </div>
      ` : ''}
      ${isSeries && data.episodeTitle ? `
        <div class="info-row">
          <span class="info-label">Nombre del episodio</span>
          <span class="info-value">${data.episodeTitle}</span>
        </div>
      ` : ''}
      ${progressValue ? `
        <div class="info-row">
          <span class="info-label">Progreso</span>
          <span class="info-value">${progressValue}</span>
        </div>
      ` : ''}
      ${totalDuration ? `
        <div class="info-row">
          <span class="info-label">Duración total</span>
          <span class="info-value">${totalDuration}</span>
        </div>
      ` : ''}
      ${data.genres && data.genres.length > 0 ? `
        <div class="info-row">
          <span class="info-label">Géneros</span>
          <span class="info-value">${data.genres.join(', ')}</span>
        </div>
      ` : ''}
      ${data.description ? `
        <div class="info-row">
          <span class="info-label">Descripción</span>
          <span class="info-value">${data.description}</span>
        </div>
      ` : ''}
    </div>
    <details>
      <summary>Ver JSON</summary>
      <pre>${JSON.stringify(data, null, 2)}</pre>
    </details>
  `;
}
