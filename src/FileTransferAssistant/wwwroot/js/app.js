const state = {
    maxFileSize: 0,
    isUploading: false
};

const dom = {
    statusDot: document.getElementById('statusDot'),
    statusText: document.getElementById('statusText'),
    serverUrl: document.getElementById('serverUrl'),
    uploadLimit: document.getElementById('uploadLimit'),
    dropZone: document.getElementById('dropZone'),
    fileInput: document.getElementById('fileInput'),
    refreshButton: document.getElementById('refreshButton'),
    progressPanel: document.getElementById('progressPanel'),
    progressName: document.getElementById('progressName'),
    progressPercent: document.getElementById('progressPercent'),
    progressBar: document.getElementById('progressBar'),
    progressSize: document.getElementById('progressSize'),
    notice: document.getElementById('notice'),
    inboxMeta: document.getElementById('inboxMeta'),
    fileList: document.getElementById('fileList')
};

const api = {
    async getStatus() {
        const response = await fetch('/api/status');
        if (!response.ok) throw new Error(`状态读取失败：${response.status}`);
        return response.json();
    },

    async listFiles() {
        const response = await fetch('/api/files');
        if (!response.ok) throw new Error(`接收箱读取失败：${response.status}`);
        return response.json();
    },

    uploadFiles(files, onProgress) {
        const formData = new FormData();
        for (const file of files) {
            formData.append('files', file);
        }

        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();

            xhr.upload.addEventListener('progress', event => {
                if (!event.lengthComputable) return;
                onProgress(event.loaded, event.total);
            });

            xhr.addEventListener('load', () => {
                if (xhr.status < 200 || xhr.status >= 300) {
                    reject(new Error(`上传失败：${xhr.status}`));
                    return;
                }

                try {
                    resolve(JSON.parse(xhr.responseText));
                } catch {
                    reject(new Error('服务器返回格式无效'));
                }
            });

            xhr.addEventListener('error', () => reject(new Error('网络连接中断')));
            xhr.open('POST', '/api/upload');
            xhr.send(formData);
        });
    }
};

const ui = {
    setConnected(status) {
        dom.statusDot.classList.toggle('connected', status);
        dom.statusText.textContent = status ? '在线' : '离线';
    },

    showNotice(message, tone = 'neutral') {
        dom.notice.textContent = message;
        dom.notice.dataset.tone = tone;
        dom.notice.hidden = false;
        clearTimeout(this.noticeTimer);
        this.noticeTimer = setTimeout(() => {
            dom.notice.hidden = true;
        }, 4200);
    },

    setProgress(name, loaded, total) {
        const percent = total > 0 ? Math.round((loaded / total) * 100) : 0;
        dom.progressPanel.hidden = false;
        dom.progressName.textContent = name;
        dom.progressPercent.textContent = `${percent}%`;
        dom.progressBar.style.width = `${percent}%`;
        dom.progressSize.textContent = `${formatFileSize(loaded)} / ${formatFileSize(total)}`;
    },

    hideProgress() {
        dom.progressPanel.hidden = true;
        dom.progressBar.style.width = '0%';
    },

    renderFiles(files) {
        dom.inboxMeta.textContent = files.length === 0
            ? '暂无文件'
            : `${files.length} 个文件`;

        dom.fileList.replaceChildren();

        if (files.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'empty-state';
            empty.textContent = '接收箱为空';
            dom.fileList.appendChild(empty);
            return;
        }

        for (const file of files) {
            const row = document.createElement('article');
            row.className = 'file-row';

            const info = document.createElement('div');
            info.className = 'file-info';

            const name = document.createElement('a');
            name.className = 'file-name';
            name.href = file.downloadUrl;
            name.textContent = file.fileName;

            const meta = document.createElement('div');
            meta.className = 'file-meta';
            meta.textContent = `${file.formattedSize} · ${formatDate(file.receivedAt)}`;

            info.append(name, meta);

            const action = document.createElement('a');
            action.className = 'download-button';
            action.href = file.downloadUrl;
            action.textContent = '下载';

            row.append(info, action);
            dom.fileList.appendChild(row);
        }
    }
};

async function refreshStatus() {
    try {
        const status = await api.getStatus();
        state.maxFileSize = status.maxFileSize || 0;
        dom.serverUrl.textContent = status.serverUrl || window.location.href;
        dom.uploadLimit.textContent = `单文件上限 ${status.maxFileSizeFormatted || '未限制'}`;
        ui.setConnected(true);
    } catch (error) {
        ui.setConnected(false);
        dom.serverUrl.textContent = '无法连接到本机服务';
        console.error(error);
    }
}

async function refreshFiles() {
    try {
        const data = await api.listFiles();
        ui.renderFiles(data.files || []);
    } catch (error) {
        ui.showNotice(error.message, 'error');
        console.error(error);
    }
}

async function handleFiles(files) {
    const selected = Array.from(files || []);
    if (selected.length === 0 || state.isUploading) return;

    const oversized = selected.find(file => state.maxFileSize > 0 && file.size > state.maxFileSize);
    if (oversized) {
        ui.showNotice(`${oversized.name} 超过单文件上限`, 'error');
        return;
    }

    state.isUploading = true;
    dom.dropZone.classList.add('busy');
    ui.setProgress(selected.length === 1 ? selected[0].name : `${selected.length} 个文件`, 0, selected.reduce((sum, file) => sum + file.size, 0));

    try {
        const result = await api.uploadFiles(selected, (loaded, total) => {
            ui.setProgress(selected.length === 1 ? selected[0].name : `${selected.length} 个文件`, loaded, total);
        });

        const failed = (result.files || []).filter(item => !item.success);
        if (failed.length > 0) {
            ui.showNotice(`${failed.length} 个文件未上传成功`, 'error');
        } else {
            ui.showNotice('文件已接收', 'success');
        }

        await refreshFiles();
    } catch (error) {
        ui.showNotice(error.message, 'error');
        console.error(error);
    } finally {
        state.isUploading = false;
        dom.dropZone.classList.remove('busy');
        dom.fileInput.value = '';
        setTimeout(() => ui.hideProgress(), 600);
    }
}

function formatFileSize(bytes) {
    if (!bytes) return '0 B';
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    const index = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
    const value = bytes / Math.pow(1024, index);
    return `${Math.round(value * 100) / 100} ${units[index]}`;
}

function formatDate(value) {
    const date = new Date(value);
    return date.toLocaleString([], {
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function setupEvents() {
    dom.fileInput.addEventListener('change', event => handleFiles(event.target.files));
    dom.refreshButton.addEventListener('click', refreshFiles);

    for (const eventName of ['dragenter', 'dragover']) {
        dom.dropZone.addEventListener(eventName, event => {
            event.preventDefault();
            dom.dropZone.classList.add('dragging');
        });
    }

    for (const eventName of ['dragleave', 'drop']) {
        dom.dropZone.addEventListener(eventName, event => {
            event.preventDefault();
            dom.dropZone.classList.remove('dragging');
        });
    }

    dom.dropZone.addEventListener('drop', event => {
        handleFiles(event.dataTransfer.files);
    });
}

async function init() {
    setupEvents();
    await refreshStatus();
    await refreshFiles();
    setInterval(refreshStatus, 5000);
}

init();
