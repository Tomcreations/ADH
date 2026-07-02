const DOWNLOAD_OPTIONS = [
  {
    label: 'itch.io',
    href: 'https://tomcreations.itch.io/adh',
    note: 'Best for the project page and downloads hosted there.',
  },
  {
    label: 'GitHub',
    href: 'https://github.com/Tomcreations/ADH',
    note: 'Best for the source repository and release files.',
  },
];

const elements = {
  downloadButton: document.querySelector('[data-download-button]'),
  downloadDialog: document.querySelector('[data-download-dialog]'),
  closeButton: document.querySelector('[data-download-close]'),
  optionList: document.querySelector('[data-download-options]'),
};

function openDialog() {
  elements.downloadDialog?.showModal();
}

function closeDialog() {
  elements.downloadDialog?.close();
}

function renderOptions() {
  if (!elements.optionList) return;

  elements.optionList.innerHTML = DOWNLOAD_OPTIONS.map(
    (option) => `
      <a class="download-option" href="${option.href}" target="_blank" rel="noreferrer">
        <span class="download-option-label">${option.label}</span>
        <span class="download-option-note">${option.note}</span>
      </a>
    `,
  ).join('');
}

elements.downloadButton?.addEventListener('click', openDialog);
elements.closeButton?.addEventListener('click', closeDialog);
elements.downloadDialog?.addEventListener('click', (event) => {
  const rect = elements.downloadDialog.getBoundingClientRect();
  const inside =
    rect.top <= event.clientY &&
    event.clientY <= rect.top + rect.height &&
    rect.left <= event.clientX &&
    event.clientX <= rect.left + rect.width;

  if (!inside) closeDialog();
});

document.addEventListener('keydown', (event) => {
  if (event.key === 'Escape') closeDialog();
});

renderOptions();
