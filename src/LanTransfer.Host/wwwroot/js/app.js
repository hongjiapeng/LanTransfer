const state = {
    selectedFiles: [],
    accessToken: '',
    isUploading: false
};

const dom = {
    deviceName: document.getElementById('deviceName'),
    timeline: document.getElementById('timeline'),
    emptyState: document.getElementById('emptyState'),
    fileInput: document.getElementById('fileInput'),
    chooseButton: document.getElementById('chooseButton'),
    sendButton: document.getElementById('sendButton'),
    dropZone: document.getElementById('dropZone'),
    languageSelect: document.getElementById('languageSelect'),
    tokenInput: document.getElementById('tokenInput'),
    toast: document.getElementById('toast')
};

function t(key, params) {
    return window.lanTransferI18n.t(key, params);
}

function withToken(url) {
    if (!state.accessToken) {
        return url;
    }

    const separator = url.includes('?') ? '&' : '?';
    return `${url}${separator}token=${encodeURIComponent(state.accessToken)}`;
}

function authHeaders() {
    return state.accessToken ? { 'X-LanTransfer-Token': state.accessToken } : {};
}

async function readError(response) {
    try {
        const payload = await response.json();
        return payload.errorCode || 'network_error';
    } catch {
        return 'network_error';
    }
}

async function getHealth() {
    const response = await fetch('/api/health');
    if (!response.ok) {
        throw new Error('network_error');
    }

    return response.json();
}

async function listFiles() {
    const response = await fetch(withToken('/api/files'), {
        headers: authHeaders()
    });

    if (!response.ok) {
        throw new Error(await readError(response));
    }

    return response.json();
}

function uploadFile(file, onProgress) {
    const formData = new FormData();
    formData.append('file', file);

    return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();

        xhr.upload.addEventListener('progress', event => {
            if (event.lengthComputable) {
                onProgress(Math.round((event.loaded / event.total) * 100));
            }
        });

        xhr.addEventListener('load', () => {
            if (xhr.status < 200 || xhr.status >= 300) {
                try {
                    const payload = JSON.parse(xhr.responseText);
                    reject(new Error(payload.errorCode || 'upload_failed'));
                } catch {
                    reject(new Error('upload_failed'));
                }
                return;
            }

            resolve(JSON.parse(xhr.responseText));
        });

        xhr.addEventListener('error', () => reject(new Error('network_error')));
        xhr.open('POST', withToken('/api/files/upload'));
        if (state.accessToken) {
            xhr.setRequestHeader('X-LanTransfer-Token', state.accessToken);
        }
        xhr.send(formData);
    });
}

function renderFileMessage(file, direction = 'incoming') {
    const row = document.createElement('article');
    row.className = `message-row ${direction}`;

    const time = document.createElement('time');
    time.className = 'message-time';
    time.textContent = formatTime(file.lastModifiedTime || new Date());

    const card = document.createElement('div');
    card.className = 'file-card';

    const visual = createVisual(file);
    const content = document.createElement('div');

    const title = document.createElement('h2');
    title.className = 'file-title';
    title.textContent = file.fileName;

    const meta = document.createElement('p');
    meta.className = 'file-meta';
    meta.textContent = `${formatFileSize(file.size)} · ${fileType(file.fileName)}`;

    const action = document.createElement('div');
    action.className = 'file-action';

    if (direction === 'incoming') {
        const link = document.createElement('a');
        link.className = 'download-button';
        link.href = withToken(file.downloadUrl);
        link.textContent = `↓ ${t('files.download')}`;
        action.appendChild(link);
    } else {
        const status = document.createElement('span');
        status.className = 'status-text';
        status.textContent = `✓ ${t('upload.sent')}`;
        action.appendChild(status);
    }

    content.append(title, meta, action);
    card.append(visual, content);

    if (direction === 'outgoing') {
        row.append(time, card);
    } else {
        row.append(card, time);
    }

    dom.timeline.appendChild(row);
    dom.timeline.scrollTop = dom.timeline.scrollHeight;
    return row;
}

function renderUploadMessage(file) {
    const row = document.createElement('article');
    row.className = 'message-row outgoing';

    const time = document.createElement('time');
    time.className = 'message-time';
    time.textContent = formatTime(new Date());

    const card = document.createElement('div');
    card.className = 'file-card';

    const content = document.createElement('div');
    const title = document.createElement('h2');
    title.className = 'file-title';
    title.textContent = file.name;

    const meta = document.createElement('p');
    meta.className = 'file-meta';
    meta.textContent = `${formatFileSize(file.size)} · ${fileType(file.name)}`;

    const action = document.createElement('div');
    action.className = 'file-action';

    const progress = document.createElement('div');
    progress.className = 'progress-track';
    const bar = document.createElement('div');
    bar.className = 'progress-bar';
    progress.appendChild(bar);

    const status = document.createElement('span');
    status.className = 'status-text';
    status.textContent = t('upload.sending', { percent: 0 });

    action.append(progress, status);
    content.append(title, meta, action);
    card.append(createVisual({ fileName: file.name, downloadUrl: URL.createObjectURL(file), isLocal: true }), content);
    row.append(time, card);
    dom.timeline.appendChild(row);
    dom.timeline.scrollTop = dom.timeline.scrollHeight;

    return {
        setProgress(percent) {
            bar.style.width = `${percent}%`;
            status.textContent = t('upload.sending', { percent });
        },
        setSent(result) {
            URL.revokeObjectURL(card.querySelector('img')?.src || '');
            status.textContent = `✓ ${t('upload.sent')}`;
            progress.remove();
            title.textContent = result.fileName;
            meta.textContent = `${formatFileSize(result.size)} · ${fileType(result.fileName)}`;
        },
        setFailed(errorCode) {
            status.classList.add('failed');
            status.textContent = t(errorKey(errorCode));
            progress.remove();
        }
    };
}

function createVisual(file) {
    if (isImage(file.fileName)) {
        const img = document.createElement('img');
        img.className = 'file-thumb';
        img.alt = '';
        img.src = file.isLocal ? file.downloadUrl : withToken(file.downloadUrl);
        img.addEventListener('error', () => {
            img.replaceWith(createFileIcon(file.fileName));
        }, { once: true });
        return img;
    }

    return createFileIcon(file.fileName);
}

function createFileIcon(fileName) {
    const icon = document.createElement('div');
    icon.className = 'file-icon';
    icon.textContent = fileType(fileName);
    return icon;
}

async function refreshHealth() {
    try {
        const health = await getHealth();
        dom.deviceName.textContent = health.deviceName || t('device.defaultName');
    } catch {
        dom.deviceName.textContent = t('device.defaultName');
    }
}

async function refreshFiles() {
    try {
        const files = await listFiles();
        dom.timeline.replaceChildren();
        dom.emptyState.hidden = files.length > 0;
        for (const file of [...files].reverse()) {
            renderFileMessage(file, 'incoming');
        }
    } catch (error) {
        showToast(t(errorKey(error.message)), true);
    }
}

async function sendFiles(files) {
    const selected = Array.from(files || []);
    if (selected.length === 0 || state.isUploading) {
        return;
    }

    state.isUploading = true;
    dom.emptyState.hidden = true;

    for (const file of selected) {
        const message = renderUploadMessage(file);
        try {
            const result = await uploadFile(file, percent => message.setProgress(percent));
            message.setSent(result);
        } catch (error) {
            message.setFailed(error.message);
            showToast(t(errorKey(error.message)), true);
        }
    }

    state.isUploading = false;
    dom.fileInput.value = '';
}

function chooseFiles() {
    dom.fileInput.click();
}

function setupEvents() {
    dom.chooseButton.addEventListener('click', chooseFiles);
    dom.dropZone.addEventListener('click', chooseFiles);
    dom.sendButton.addEventListener('click', () => chooseFiles());
    dom.fileInput.addEventListener('change', event => sendFiles(event.target.files));

    for (const eventName of ['dragenter', 'dragover']) {
        document.addEventListener(eventName, event => {
            event.preventDefault();
            dom.dropZone.classList.add('dragging');
        });
    }

    for (const eventName of ['dragleave', 'drop']) {
        document.addEventListener(eventName, event => {
            event.preventDefault();
            dom.dropZone.classList.remove('dragging');
        });
    }

    document.addEventListener('drop', event => {
        sendFiles(event.dataTransfer.files);
    });

    dom.languageSelect.addEventListener('change', async event => {
        await window.lanTransferI18n.setLanguage(event.target.value);
        renderStaticState();
        await refreshFiles();
    });

    dom.tokenInput.addEventListener('change', event => {
        state.accessToken = event.target.value.trim();
        localStorage.setItem('lantransfer.token', state.accessToken);
        refreshFiles();
    });
}

function renderStaticState() {
    document.title = t('app.title');
    if (!dom.deviceName.textContent || dom.deviceName.textContent === 'DESKTOP-01') {
        dom.deviceName.textContent = t('device.defaultName');
    }
}

function showToast(message, isError = false) {
    dom.toast.textContent = message;
    dom.toast.classList.toggle('error', isError);
    dom.toast.hidden = false;
    clearTimeout(showToast.timer);
    showToast.timer = setTimeout(() => {
        dom.toast.hidden = true;
    }, 3600);
}

function errorKey(errorCode) {
    const map = {
        file_too_large: 'errors.fileTooLarge',
        file_not_found: 'errors.fileNotFound',
        invalid_file_name: 'errors.invalidFileName',
        unauthorized: 'errors.unauthorized',
        upload_failed: 'errors.uploadFailed',
        network_error: 'errors.networkError'
    };

    return map[errorCode] || 'errors.networkError';
}

function formatFileSize(bytes) {
    if (!bytes) {
        return '0 B';
    }

    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    const index = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
    const value = bytes / Math.pow(1024, index);
    return `${Math.round(value * 100) / 100} ${units[index]}`;
}

function formatTime(value) {
    return new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

function fileType(fileName) {
    const extension = (fileName.split('.').pop() || 'file').toUpperCase();
    return extension.length > 6 ? 'FILE' : extension;
}

function isImage(fileName) {
    return /\.(png|jpe?g|gif|webp|bmp|avif)$/i.test(fileName);
}

async function init() {
    const params = new URLSearchParams(window.location.search);
    state.accessToken = params.get('token') || localStorage.getItem('lantransfer.token') || '';
    dom.tokenInput.value = state.accessToken;

    await window.lanTransferI18n.init();
    dom.languageSelect.value = window.lanTransferI18n.language;
    renderStaticState();
    setupEvents();
    await refreshHealth();
    await refreshFiles();
}

init();
