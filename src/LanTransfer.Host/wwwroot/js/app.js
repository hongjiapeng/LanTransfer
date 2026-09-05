const state = {
    accessToken: '',
    isUploading: false,
    isSendingMessage: false,
    connectionStatus: 'connecting',
    canRevealFiles: false,
    canManageHistory: false,
    pendingFiles: [],
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
    pendingAttachments: document.getElementById('pendingAttachments'),
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
    copyConnectUrl: document.getElementById('copyConnectUrl'),
    deleteDialog: document.getElementById('deleteDialog'),
    deleteDescription: document.getElementById('deleteDescription')
};

let openFileMenuState = null;

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

function isLoopbackHostname(hostname) {
    const normalized = hostname.toLowerCase();
    return normalized === 'localhost' || normalized === '127.0.0.1' || normalized === '::1' || normalized === '[::1]';
}

async function getShareableBaseUrl() {
    const currentUrl = new URL(window.location.href);
    if (!isLoopbackHostname(currentUrl.hostname)) {
        return currentUrl;
    }

    let lanUrl = state.connectUrls.find(item => item.isLanAddress)?.url;
    if (!lanUrl) {
        try {
            const payload = await fetchJson('/api/connect');
            state.connectUrls = payload.urls || [];
            lanUrl = state.connectUrls.find(item => item.isLanAddress)?.url;
        } catch {
            // Keep localhost as a fallback when no LAN address can be detected.
        }
    }

    return lanUrl ? new URL(lanUrl) : currentUrl;
}

async function getShareableFileUrl(file) {
    const fileUrl = new URL(file.downloadUrl, window.location.href);
    const shareableBaseUrl = await getShareableBaseUrl();
    fileUrl.protocol = shareableBaseUrl.protocol;
    fileUrl.host = shareableBaseUrl.host;
    return withToken(fileUrl.toString());
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

function confirmDelete(description) {
    dom.deleteDescription.textContent = description;
    dom.deleteDialog.returnValue = 'cancel';
    dom.deleteDialog.showModal();

    return new Promise(resolve => {
        dom.deleteDialog.addEventListener('close', () => {
            resolve(dom.deleteDialog.returnValue === 'confirm');
        }, { once: true });
    });
}

async function deleteFile(file) {
    if (!await confirmDelete(t('delete.filePrompt', { name: file.fileName }))) {
        return;
    }

    try {
        await fetchJson(`/api/files/${encodeURIComponent(file.fileName)}`, { method: 'DELETE' });
        showToast(t('files.deleted'));
        await refreshTimeline();
    } catch (error) {
        showToast(t(errorKey(error.message)), true);
    }
}

async function deleteMessage(message) {
    if (!await confirmDelete(t('delete.messagePrompt'))) {
        return;
    }

    try {
        await fetchJson(`/api/messages/${encodeURIComponent(message.id)}`, { method: 'DELETE' });
        state.sentMessageIds.delete(message.id);
        persistSentMessageIds();
        showToast(t('messages.deleted'));
        await refreshTimeline();
    } catch (error) {
        showToast(t(errorKey(error.message)), true);
    }
}

function closeFileMenu({ restoreFocus = false } = {}) {
    if (!openFileMenuState) {
        return;
    }

    const { menu, trigger } = openFileMenuState;
    menu.remove();
    trigger?.setAttribute('aria-expanded', 'false');
    openFileMenuState = null;

    if (restoreFocus && trigger?.isConnected) {
        trigger.focus({ preventScroll: true });
    }
}

function createFileMenuItem(label, { href, onSelect, danger = false } = {}) {
    const item = document.createElement(href ? 'a' : 'button');
    item.className = 'file-menu-item';
    item.classList.toggle('danger', danger);
    item.setAttribute('role', 'menuitem');
    item.textContent = label;

    if (href) {
        item.href = href;
        item.download = '';
        item.addEventListener('click', () => closeFileMenu());
    } else {
        item.type = 'button';
        item.addEventListener('click', async () => {
            closeFileMenu();
            await onSelect?.();
        });
    }

    return item;
}

function createMenuSeparator() {
    const separator = document.createElement('div');
    separator.className = 'file-menu-separator';
    separator.setAttribute('role', 'separator');
    return separator;
}

function showFileMenu(menu, { x, y, trigger = null, alignRight = false }) {
    document.body.appendChild(menu);
    const bounds = menu.getBoundingClientRect();
    const edge = 8;
    const preferredLeft = alignRight ? x - bounds.width : x;
    const left = Math.max(edge, Math.min(preferredLeft, window.innerWidth - bounds.width - edge));
    const top = Math.max(edge, Math.min(y, window.innerHeight - bounds.height - edge));
    menu.style.left = `${left}px`;
    menu.style.top = `${top}px`;

    trigger?.setAttribute('aria-expanded', 'true');
    openFileMenuState = { menu, trigger };
    menu.querySelector('[role="menuitem"]')?.focus({ preventScroll: true });
}

function openFileMenu(file, { x, y, trigger = null, alignRight = false }) {
    closeFileMenu();

    const menu = document.createElement('div');
    menu.className = 'file-context-menu';
    menu.setAttribute('role', 'menu');
    menu.setAttribute('aria-label', t('files.actions'));

    const downloadUrl = withToken(file.downloadUrl);
    menu.appendChild(createFileMenuItem(t('files.download'), { href: downloadUrl }));
    menu.appendChild(createFileMenuItem(t('files.copyLink'), {
        onSelect: async () => {
            const shareableUrl = await getShareableFileUrl(file);
            await copyText(shareableUrl, 'files.linkCopied');
        }
    }));

    if (state.canRevealFiles) {
        menu.appendChild(createFileMenuItem(t('files.reveal'), {
            onSelect: () => revealFile(file.fileName)
        }));
    }

    if (state.canManageHistory) {
        menu.appendChild(createMenuSeparator());
        menu.appendChild(createFileMenuItem(t('delete.action'), {
            danger: true,
            onSelect: () => deleteFile(file)
        }));
    }

    showFileMenu(menu, { x, y, trigger, alignRight });
}

function openTextMenu(message, { x, y, trigger = null, alignRight = false }) {
    closeFileMenu();

    const menu = document.createElement('div');
    menu.className = 'file-context-menu';
    menu.setAttribute('role', 'menu');
    menu.setAttribute('aria-label', t('messages.actions'));
    menu.appendChild(createFileMenuItem(t('delete.action'), {
        danger: true,
        onSelect: () => deleteMessage(message)
    }));

    showFileMenu(menu, { x, y, trigger, alignRight });
}

function attachItemMenu(card, label, openMenu) {
    const trigger = document.createElement('button');
    trigger.className = 'file-menu-trigger';
    trigger.type = 'button';
    trigger.setAttribute('aria-haspopup', 'menu');
    trigger.setAttribute('aria-expanded', 'false');
    trigger.setAttribute('aria-label', label);
    trigger.title = label;
    trigger.innerHTML = '<span aria-hidden="true">•••</span>';

    trigger.addEventListener('click', event => {
        event.stopPropagation();
        if (openFileMenuState?.trigger === trigger) {
            closeFileMenu({ restoreFocus: true });
            return;
        }

        const bounds = trigger.getBoundingClientRect();
        openMenu({
            x: bounds.right,
            y: bounds.bottom + 6,
            trigger,
            alignRight: true
        });
    });

    card.addEventListener('contextmenu', event => {
        event.preventDefault();
        openMenu({ x: event.clientX, y: event.clientY, trigger });
    });

    card.appendChild(trigger);
}

function attachFileMenu(card, file) {
    attachItemMenu(card, t('files.actions'), options => openFileMenu(file, options));
}

function attachTextMenu(card, message) {
    attachItemMenu(card, t('messages.actions'), options => openTextMenu(message, options));
}

function renderFileMessage(file, direction = 'incoming') {
    const row = document.createElement('article');
    row.className = `message-row ${direction}`;
    const time = createTime(file.lastModifiedTime || new Date());

    const card = document.createElement('div');
    card.className = 'file-card';

    const visual = createVisual(file);
    const content = document.createElement('div');
    content.className = 'file-card-content';

    const title = document.createElement('h2');
    title.className = 'file-title';
    title.textContent = file.fileName;

    const meta = document.createElement('p');
    meta.className = 'file-meta';
    meta.textContent = `${formatFileSize(file.size)} · ${fileType(file.fileName)}`;

    if (direction === 'incoming') {
        content.append(title, meta);
    } else {
        const action = document.createElement('div');
        action.className = 'file-action';
        const status = document.createElement('span');
        status.className = 'status-text';
        status.textContent = `✓ ${t('upload.sent')}`;
        action.appendChild(status);
        content.append(title, meta, action);
    }

    card.append(visual, content);
    if (direction === 'incoming') {
        attachFileMenu(card, file);
    }
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
    if (state.canManageHistory) {
        attachTextMenu(card, message);
    }

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
    if (state.isUploading || state.isSendingMessage || openFileMenuState) {
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

function extensionForMimeType(mimeType) {
    const extensions = {
        'image/jpeg': 'jpg',
        'image/png': 'png',
        'image/gif': 'gif',
        'image/webp': 'webp',
        'image/bmp': 'bmp',
        'image/svg+xml': 'svg',
        'application/pdf': 'pdf',
        'text/plain': 'txt',
        'text/csv': 'csv',
        'application/zip': 'zip'
    };

    return extensions[mimeType] || (mimeType?.split('/').pop()?.replace(/[^a-z0-9]/gi, '') || 'bin');
}

function normalizeClipboardFile(file, index) {
    if (file.name) {
        return file;
    }

    const mimeType = file.type || 'application/octet-stream';
    const prefix = mimeType.startsWith('image/') ? 'pasted-image' : 'pasted-file';
    const extension = extensionForMimeType(mimeType);
    return new File([file], `${prefix}-${Date.now()}-${index + 1}.${extension}`, {
        type: mimeType,
        lastModified: Date.now()
    });
}

function getClipboardFiles(clipboardData) {
    const files = Array.from(clipboardData?.files || []);
    if (files.length > 0) {
        return files.map(normalizeClipboardFile);
    }

    return Array.from(clipboardData?.items || [])
        .filter(item => item.kind === 'file')
        .map(item => item.getAsFile())
        .filter(Boolean)
        .map(normalizeClipboardFile);
}

function renderPendingFiles() {
    dom.pendingAttachments.replaceChildren();
    dom.pendingAttachments.hidden = state.pendingFiles.length === 0;

    for (const [index, file] of state.pendingFiles.entries()) {
        const attachment = document.createElement('div');
        attachment.className = 'pending-attachment';
        attachment.setAttribute('role', 'listitem');

        const icon = document.createElement('span');
        icon.className = 'pending-attachment-icon';
        icon.textContent = fileType(file.name);

        const details = document.createElement('span');
        details.className = 'pending-attachment-details';

        const name = document.createElement('span');
        name.className = 'pending-attachment-name';
        name.textContent = file.name;

        const size = document.createElement('span');
        size.className = 'pending-attachment-size';
        size.textContent = formatFileSize(file.size);
        details.append(name, size);

        const remove = document.createElement('button');
        remove.className = 'pending-attachment-remove';
        remove.type = 'button';
        remove.textContent = '×';
        remove.title = t('composer.removeAttachment', { name: file.name });
        remove.setAttribute('aria-label', remove.title);
        remove.addEventListener('click', () => {
            state.pendingFiles.splice(index, 1);
            renderPendingFiles();
            dom.messageInput.focus();
        });

        attachment.append(icon, details, remove);
        dom.pendingAttachments.appendChild(attachment);
    }
}

function handleMessagePaste(event) {
    const files = getClipboardFiles(event.clipboardData);
    if (files.length === 0) {
        return;
    }

    event.preventDefault();
    state.pendingFiles.push(...files);
    renderPendingFiles();
}

async function sendComposer() {
    const text = dom.messageInput.value.trim();
    const files = [...state.pendingFiles];
    if ((!text && files.length === 0) || state.isUploading || state.isSendingMessage) {
        if (!text && files.length === 0) {
            showToast(t('errors.invalidMessage'), true);
        }
        return;
    }

    state.pendingFiles = [];
    renderPendingFiles();

    if (text) {
        await sendText();
    }
    if (files.length > 0) {
        await sendFiles(files);
    }
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
    dom.sendButton.addEventListener('click', sendComposer);
    dom.fileInput.addEventListener('change', event => sendFiles(event.target.files));
    dom.messageInput.addEventListener('input', resizeMessageInput);
    dom.messageInput.addEventListener('paste', handleMessagePaste);
    dom.messageInput.addEventListener('keydown', event => {
        if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
            event.preventDefault();
            sendComposer();
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

    document.addEventListener('pointerdown', event => {
        if (openFileMenuState &&
            !openFileMenuState.menu.contains(event.target) &&
            !openFileMenuState.trigger?.contains(event.target)) {
            closeFileMenu();
        }
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && openFileMenuState) {
            event.preventDefault();
            closeFileMenu({ restoreFocus: true });
        }
    });
    window.addEventListener('resize', () => closeFileMenu());
    let timelineScrollEndTimer;
    dom.timeline.addEventListener('scroll', () => {
        closeFileMenu();
        dom.timeline.classList.add('is-scrolling');
        clearTimeout(timelineScrollEndTimer);
        timelineScrollEndTimer = setTimeout(() => {
            dom.timeline.classList.remove('is-scrolling');
        }, 700);
    }, { passive: true });

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
    dom.pendingAttachments.setAttribute('aria-label', t('composer.pendingAttachments'));
    renderPendingFiles();
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
        message_not_found: 'errors.messageNotFound',
        message_failed: 'errors.messageFailed',
        delete_failed: 'errors.deleteFailed',
        host_only_action: 'errors.hostOnlyAction',
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
        state.canManageHistory = Boolean(capabilities.canManageHistory);
    } catch {
        state.canRevealFiles = false;
        state.canManageHistory = false;
    }
    await refreshTimeline();
    window.setInterval(() => {
        refreshHealth();
        refreshTimeline({ preserveScroll: true });
    }, 3000);
}

init();
