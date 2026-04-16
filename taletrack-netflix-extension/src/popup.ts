import type { ExtractDataMessage, ExtractDataResponse } from './types';

const extractBtn = document.getElementById('extractBtn') as HTMLButtonElement;
const resultDiv = document.getElementById('result') as HTMLDivElement;

extractBtn.addEventListener('click', async () => {
  resultDiv.innerHTML = '<p class="loading">⏳ Extrayendo datos...</p>';
  extractBtn.disabled = true;

  try {
    // Get current tab
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });

    // Basic tab checks
    if (!tab) {
      throw new Error('No se pudo obtener la pestaña activa');
    }

    if (!tab.id) {
      throw new Error('La pestaña no tiene ID');
    }

    if (!tab.url) {
      throw new Error('La pestaña no tiene URL');
    }

    // Only run on Netflix
    if (!tab.url.includes('netflix.com')) {
      resultDiv.innerHTML = '<p class="error">❌ Por favor, abre una página de Netflix primero.</p>';
      extractBtn.disabled = false;
      return;
    }

    // Ask content script for data
    const message: ExtractDataMessage = { action: 'extractData' };

    chrome.tabs.sendMessage(
      tab.id,
      message,
      (response: ExtractDataResponse) => {
        if (chrome.runtime.lastError) {
          resultDiv.innerHTML = `<p class="error">❌ Error:  ${chrome.runtime.lastError.message}</p>`;
          extractBtn.disabled = false;
          return;
        }

        if (response.success && response.data) {
          displayData(response.data);
        } else {
          resultDiv.innerHTML = `<p class="error">❌ ${response.error || 'Error desconocido'}</p>`;
        }

        extractBtn.disabled = false;
      }
    );

  } catch (error) {
    resultDiv.innerHTML = `<p class="error">❌ Error: ${(error as Error).message}</p>`;
    extractBtn.disabled = false;
  }
});

function displayData(data: any) {
  const isSeries = data.type === 'series';

  resultDiv.innerHTML = `
    <p class="success">Se extrajeron bien los datos</p>
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
      ${data.genres.length > 0 ? `
        <div class="info-row">
          <span class="info-label">Géneros</span>
          <span class="info-value">${data.genres.join(', ')}</span>
        </div>
      ` : ''}
      ${data.duration ? `
        <div class="info-row">
          <span class="info-label">Duración</span>
          <span class="info-value">${data.duration}</span>
        </div>
      ` : ''}
      ${data.description ? `
        <div class="info-row">
          <span class="info-label">Descripción</span>
          <span class="info-value">${data.description}</span>
        </div>
      ` : ''}
    </div>
    <details style="margin-top: 16px;">
      <summary style="cursor: pointer; color: #e50914;">Ver JSON</summary>
      <pre>${JSON.stringify(data, null, 2)}</pre>
    </details>
  `;

  console.log('📊 Datos extraídos:', data);
}