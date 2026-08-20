const state = {
    accessToken: '',
    isUploading: false,
    isSendingMessage: false,
    connectionStatus: 'connecting',
    canRevealFiles: false,
    sentMessageIds: new Set(),
    connectUrls: []
};

const dom = {
    deviceName: document.getElementById('deviceName'),
    deviceStatus: document.getElementById('deviceStatus'),
    deviceStatusText: document.getElementById('deviceStatusText'),
    timeline: document.getElementById('timeline'),
    emptyState: document.getElementById('emptyState'),
    fileInput: document.getElementById('fileInput'),
    chooseButton: document.getElementById('chooseButton'),
    sendButton: document.getElementById('sendButton'),
    messageInput: document.getElementById('messageInput'),
    languageSelect: document.getElementById('languageSelect'),
    tokenInput: document.getElementById('tokenInput'),
    toast: document.getElementById('toast'),
    moreMenu: document.querySelector('.more-menu'),
    connectButton: document.getElementById('connectButton'),
    connectDialog: document.getElementById('connectDialog'),
    connectLoading: document.getElementById('connectLoading'),
    connectContent: document.getElementById('connectContent'),
    connectError: document.getElementById('connectError'),
    connectQr: document.getElementById('connectQr'),
    connectUrlSelect: document.getElementById('connectUrlSelect'),
    connectUrl: document.getElementById('connectUrl'),
    connectHelp: document.getElementById('connectHelp'),
    copyConnectUrl: document.getElementById('copyConnectUrl')
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

function authHeaders(extra = {}) {
    return state.accessToken
        ? { ...extra, 'X-LanTransfer-Token': state.accessToken }
        : extra;
}

async function readError(response) {
    try {
        const payload = await response.json();
        return payload.errorCode || 'network_error';
    } catch {
        return 'network_error';
    }
}

async function fetchJson(url, options = {}) {
    let response;
    try {
        response = await fetch(withToken(url), {
            ...options,
            headers: authHeaders(options.headers || {})
        });
    } catch {
        setConnectionStatus('disconnected');
        throw new Error('network_error');
    }

    setConnectionStatus('connected');

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
            setConnectionStatus('connected');
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

        xhr.addEventListener('error', () => {
            setConnectionStatus('disconnected');
            reject(new Error('network_error'));
        });
        xhr.open('POST', withToken('/api/files/upload'));
        if (state.accessToken) {
            xhr.setRequestHeader('X-LanTransfer-Token', state.accessToken);
        }
        xhr.send(formData);
    });
}

function createTime(value) {
    const date = new Date(value);
    const time = document.createElement('time');
    time.className = 'message-time';
    time.dateTime = date.toISOString();
    time.textContent = formatTime(date);
    time.title = formatFullDateTime(date);
    return time;
}

function createDateDivider(value) {
    const divider = document.createElement('div');
    divider.className = 'date-divider';

    const label = document.createElement('strong');
    label.textContent = formatDateLabel(value);
    divider.append(document.createElement('span'), label, document.createElement('span'));
    return divider;
}

function createActionButton(label, onClick) {
    const button = document.createElement('button');
    button.className = 'file-action-button';
    button.type = 'button';
    button.textContent = label;
    button.addEventListener('click', onClick);
    return button;
}

async function copyText(value, successKey) {
    let copied = false;
    try {
        await navigator.clipboard.writeText(value);
        copied = true;
    } catch {
        const input = document.createElement('textarea');
        input.value = value;
        input.style.position = 'fixed';
        input.style.opacity = '0';
        document.body.appendChild(input);
        input.select();
        copied = document.execCommand('copy');
        input.remove();
    }

    showToast(t(copied ? successKey : 'errors.copyFailed'), !copied);
}

async function revealFile(fileName) {
    try {
        await fetchJson(`/api/files/${encodeURIComponent(fileName)}/reveal`, { method: 'POST' });
        showToast(t('files.revealed'));
    } catch (error) {
        showToast(t(errorKey(error.message)), true);
    }
}

function renderFileMessage(file, direction = 'incoming') {
    const row = document.createElement('article');
    row.className = `message-row ${direction}`;
    const time = createTime(file.lastModifiedTime || new Date());

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
        link.className = 'file-action-button';
        link.href = withToken(file.downloadUrl);
        link.textContent = `↓ ${t('files.download')}`;
        action.appendChild(link);

        const absoluteUrl = new URL(withToken(file.downloadUrl), window.location.href).toString();
        action.appendChild(createActionButton(t('files.copyLink'), () => copyText(absoluteUrl, 'files.linkCopied')));

        if (state.canRevealFiles) {
            action.appendChild(createActionButton(t('files.reveal'), () => revealFile(file.fileName)));
        }
    } else {
        const status = document.createElement('span');
        status.className = 'status-text';
        status.textContent = `✓ ${t('upload.sent')}`;
        action.appendChild(status);
    }

    content.append(title, meta, action);
    card.append(visual, content);
    row.append(...(direction === 'outgoing' ? [time, card] : [card, time]));
    dom.timeline.appendChild(row);
    return row;
}

function renderTextMessage(message) {
    const direction = state.sentMessageIds.has(message.id) ? 'outgoing' : 'incoming';
    const row = document.createElement('article');
    row.className = `message-row ${direction}`;

    const time = createTime(message.createdAt);
    const card = document.createElement('div');
    card.className = 'text-card';
    card.textContent = message.text;

    row.append(...(direction === 'outgoing' ? [time, card] : [card, time]));
    dom.timeline.appendChild(row);
}

function renderUploadMessage(file) {
    const row = document.createElement('article');
    row.className = 'message-row outgoing';
    const time = createTime(new Date());

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

    const objectUrl = URL.createObjectURL(file);
    card.append(createVisual({ fileName: file.name, downloadUrl: objectUrl, isLocal: true }), content);
    row.append(time, card);
    dom.timeline.appendChild(row);
    dom.timeline.scrollTop = dom.timeline.scrollHeight;

    return {
        setProgress(percent) {
            bar.style.width = `${percent}%`;
            status.textContent = t('upload.sending', { percent });
        },
        setSent(result) {
            URL.revokeObjectURL(objectUrl);
            status.textContent = `✓ ${t('upload.sent')}`;
            progress.remove();
            title.textContent = result.fileName;
            meta.textContent = `${formatFileSize(result.size)} · ${fileType(result.fileName)}`;
        },
        setFailed(errorCode) {
            URL.revokeObjectURL(objectUrl);
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
        img.addEventListener('error', () => img.replaceWith(createFileIcon(file.fileName)), { once: true });
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
    if (!navigator.onLine) {
        setConnectionStatus('disconnected');
        return;
    }

    try {
        const health = await fetchJson('/api/health');
        dom.deviceName.textContent = health.deviceName || t('device.defaultName');
    } catch {
        setConnectionStatus('disconnected');
    }
}

async function refreshTimeline({ preserveScroll = false } = {}) {
    if (state.isUploading || state.isSendingMessage) {
        return;
    }

    const wasNearBottom = dom.timeline.scrollHeight - dom.timeline.scrollTop - dom.timeline.clientHeight < 80;
    try {
        const [files, messages] = await Promise.all([
            fetchJson('/api/files'),
            fetchJson('/api/messages')
        ]);

        const items = [
            ...files.map(file => ({ kind: 'file', timestamp: file.lastModifiedTime, value: file })),
            ...messages.map(message => ({ kind: 'message', timestamp: message.createdAt, value: message }))
        ].sort((left, right) => new Date(left.timestamp) - new Date(right.timestamp));

        dom.timeline.replaceChildren();
        dom.emptyState.hidden = items.length > 0;
        let currentDateKey = '';
        for (const item of items) {
            const itemDateKey = localDateKey(item.timestamp);
            if (itemDateKey !== currentDateKey) {
                dom.timeline.appendChild(createDateDivider(item.timestamp));
                currentDateKey = itemDateKey;
            }

            if (item.kind === 'file') {
                renderFileMessage(item.value, 'incoming');
            } else {
                renderTextMessage(item.value);
            }
        }

        if (!preserveScroll || wasNearBottom) {
            dom.timeline.scrollTop = dom.timeline.scrollHeight;
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
    await refreshTimeline();
}

async function sendText() {
    const text = dom.messageInput.value.trim();
    if (!text || state.isSendingMessage) {
        if (!text) {
            showToast(t('errors.invalidMessage'), true);
        }
        return;
    }

    state.isSendingMessage = true;
    dom.sendButton.disabled = true;
    try {
        const message = await fetchJson('/api/messages', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ text })
        });
        state.sentMessageIds.add(message.id);
        persistSentMessageIds();
        dom.messageInput.value = '';
        resizeMessageInput();
    } catch (error) {
        showToast(t(errorKey(error.message)), true);
    } finally {
        state.isSendingMessage = false;
        dom.sendButton.disabled = false;
    }

    await refreshTimeline();
}

function resizeMessageInput() {
    dom.messageInput.style.height = '';
    dom.messageInput.style.height = `${Math.min(dom.messageInput.scrollHeight, 112)}px`;
}

async function openConnectDialog() {
    dom.moreMenu.open = false;

    if (dom.connectDialog.open) {
        dom.connectDialog.focus();
        return;
    }

    dom.connectLoading.hidden = false;
    dom.connectContent.hidden = true;
    dom.connectError.hidden = true;
    dom.connectDialog.showModal();

    try {
        const payload = await fetchJson('/api/connect');
        state.connectUrls = payload.urls || [];
        if (state.connectUrls.length === 0) {
            throw new Error('no_connect_address');
        }

        dom.connectUrlSelect.replaceChildren();
        for (const item of state.connectUrls) {
            const option = document.createElement('option');
            option.value = item.url;
            option.textContent = `${item.label} — ${item.url}`;
            dom.connectUrlSelect.appendChild(option);
        }

        updateConnectUrl();
        dom.connectLoading.hidden = true;
        dom.connectContent.hidden = false;
    } catch (error) {
        dom.connectLoading.hidden = true;
        dom.connectError.textContent = t(errorKey(error.message));
        dom.connectError.hidden = false;
    }
}

function updateConnectUrl() {
    const url = dom.connectUrlSelect.value;
    dom.connectUrl.textContent = url;
    dom.connectUrl.value = url;
    dom.connectQr.alt = t('connect.qrAlt');
    dom.connectQr.src = withToken(`/api/connect/qr?url=${encodeURIComponent(url)}`);

    const selected = state.connectUrls.find(item => item.url === url);
    dom.connectHelp.textContent = selected?.isLanAddress ? t('connect.help') : t('connect.localhostWarning');
}

async function copyConnectUrl() {
    const url = dom.connectUrlSelect.value;
    await copyText(url, 'connect.copied');
}

function setupEvents() {
    dom.chooseButton.addEventListener('click', () => dom.fileInput.click());
    dom.sendButton.addEventListener('click', sendText);
    dom.fileInput.addEventListener('change', event => sendFiles(event.target.files));
    dom.messageInput.addEventListener('input', resizeMessageInput);
    dom.messageInput.addEventListener('keydown', event => {
        if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
            event.preventDefault();
            sendText();
        }
    });

    for (const eventName of ['dragenter', 'dragover']) {
        document.addEventListener(eventName, event => {
            event.preventDefault();
            dom.messageInput.classList.add('dragging');
        });
    }

    for (const eventName of ['dragleave', 'drop']) {
        document.addEventListener(eventName, event => {
            event.preventDefault();
            dom.messageInput.classList.remove('dragging');
        });
    }

    document.addEventListener('drop', event => sendFiles(event.dataTransfer.files));

    dom.languageSelect.addEventListener('change', async event => {
        await window.lanTransferI18n.setLanguage(event.target.value);
        renderStaticState();
        await refreshTimeline();
    });

    dom.tokenInput.addEventListener('change', async event => {
        state.accessToken = event.target.value.trim();
        localStorage.setItem('lantransfer.token', state.accessToken);
        await refreshTimeline();
    });

    dom.connectButton.addEventListener('click', openConnectDialog);
    dom.connectUrlSelect.addEventListener('change', updateConnectUrl);
    dom.copyConnectUrl.addEventListener('click', copyConnectUrl);

    window.addEventListener('offline', () => setConnectionStatus('disconnected'));
    window.addEventListener('online', () => {
        setConnectionStatus('connecting');
        refreshHealth();
    });
}

function setConnectionStatus(status) {
    state.connectionStatus = status;
    dom.deviceStatus.dataset.connectionStatus = status;
    dom.deviceStatusText.textContent = t(`device.${status}`);
}

function renderStaticState() {
    document.title = t('app.title');
    setConnectionStatus(state.connectionStatus);
    dom.messageInput.setAttribute('aria-label', t('composer.placeholder'));
    if (!dom.deviceName.textContent || dom.deviceName.textContent === 'DESKTOP-01') {
        dom.deviceName.textContent = t('device.defaultName');
    }
    if (dom.connectUrlSelect.value) {
        updateConnectUrl();
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
        invalid_message: 'errors.invalidMessage',
        message_failed: 'errors.messageFailed',
        no_connect_address: 'errors.noConnectAddress',
        reveal_not_available: 'errors.revealNotAvailable',
        reveal_failed: 'errors.revealFailed',
        network_error: 'errors.networkError'
    };
    return map[errorCode] || 'errors.networkError';
}

function persistSentMessageIds() {
    const ids = [...state.sentMessageIds].slice(-500);
    localStorage.setItem('lantransfer.sentMessageIds', JSON.stringify(ids));
}

function loadSentMessageIds() {
    try {
        const ids = JSON.parse(localStorage.getItem('lantransfer.sentMessageIds') || '[]');
        state.sentMessageIds = new Set(Array.isArray(ids) ? ids : []);
    } catch {
        state.sentMessageIds = new Set();
    }
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

function formatFullDateTime(value) {
    return new Date(value).toLocaleString([], {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function localDateKey(value) {
    const date = new Date(value);
    return `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
}

function formatDateLabel(value) {
    const date = new Date(value);
    const today = new Date();
    const yesterday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 1);

    if (localDateKey(date) === localDateKey(today)) {
        return t('date.today');
    }

    if (localDateKey(date) === localDateKey(yesterday)) {
        return t('date.yesterday');
    }

    const options = date.getFullYear() === today.getFullYear()
        ? { month: 'long', day: 'numeric' }
        : { year: 'numeric', month: 'long', day: 'numeric' };
    return date.toLocaleDateString([], options);
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
    loadSentMessageIds();

    await window.lanTransferI18n.init();
    dom.languageSelect.value = window.lanTransferI18n.language;
    renderStaticState();
    setupEvents();
    await refreshHealth();
    try {
        const capabilities = await fetchJson('/api/capabilities');
        state.canRevealFiles = Boolean(capabilities.canRevealFiles);
    } catch {
        state.canRevealFiles = false;
    }
    await refreshTimeline();
    window.setInterval(() => {
        refreshHealth();
        refreshTimeline({ preserveScroll: true });
    }, 3000);
}

init();
