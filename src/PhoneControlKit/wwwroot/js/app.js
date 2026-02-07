/**
 * Xiaowei Remote Chat - Application Logic
 * Main JavaScript module for chat functionality
 */

// ==================== Module: State Management ====================
const AppState = {
    isProcessing: false,
    messageHistory: [],
    currentUpload: null,

    setProcessing(value) {
        this.isProcessing = value;
    },

    addMessage(text, isUser, fileInfo = null) {
        this.messageHistory.push({
            text,
            isUser,
            time: Utils.getCurrentTime(),
            fileInfo
        });
    },

    setCurrentUpload(upload) {
        this.currentUpload = upload;
    }
};

// ==================== Module: DOM References ====================
const DOM = {
    chatContainer: document.getElementById('chatContainer'),
    messageInput: document.getElementById('messageInput'),
    sendButton: document.getElementById('sendButton'),
    uploadButton: document.getElementById('uploadButton'),
    fileInput: document.getElementById('fileInput'),
    typingIndicator: document.getElementById('typingIndicator'),
    errorMessage: document.getElementById('errorMessage'),
    successMessage: document.getElementById('successMessage'),
    statusDot: document.getElementById('statusDot'),
    statusText: document.getElementById('statusText'),
    timestampElement: document.getElementById('timestamp'),
    uploadProgress: document.getElementById('uploadProgress'),
    progressBar: document.getElementById('progressBar'),
    progressPercent: document.getElementById('progressPercent'),
    progressSize: document.getElementById('progressSize'),
    cancelUpload: document.getElementById('cancelUpload'),
    filePreview: document.getElementById('filePreview'),
    filePreviewIcon: document.getElementById('filePreviewIcon'),
    filePreviewName: document.getElementById('filePreviewName'),
    filePreviewSize: document.getElementById('filePreviewSize')
};

// ==================== Module: Utilities ====================
const Utils = {
    getCurrentTime() {
        return new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    },

    formatFileSize(bytes) {
        const sizes = ['B', 'KB', 'MB', 'GB'];
        if (bytes === 0) return '0 B';
        const i = Math.floor(Math.log(bytes) / Math.log(1024));
        return Math.round(bytes / Math.pow(1024, i) * 100) / 100 + ' ' + sizes[i];
    },

    getFileIcon(fileName) {
        const ext = fileName.split('.').pop().toLowerCase();
        const iconMap = {
            'jpg': '🖼️', 'jpeg': '🖼️', 'png': '🖼️', 'gif': '🖼️', 'webp': '🖼️',
            'pdf': '📄',
            'doc': '📝', 'docx': '📝', 'txt': '📝',
            'zip': '📦', 'rar': '📦', '7z': '📦',
            'mp4': '🎥', 'avi': '🎥', 'mov': '🎥',
            'mp3': '🎵', 'wav': '🎵', 'flac': '🎵'
        };
        return iconMap[ext] || '📄';
    },

    isImageFile(fileName) {
        const ext = fileName.split('.').pop().toLowerCase();
        return ['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp'].includes(ext);
    }
};

// ==================== Module: UI Helpers (ChatGPT-like input behavior) ====================
const UIHelpers = {
    updateSendButtonState() {
        const hasText = DOM.messageInput.value.trim().length > 0;
        DOM.sendButton.disabled = !hasText || AppState.isProcessing;
    },

    autoResizeTextarea(el) {
        if (!el) return;
        el.style.height = 'auto';
        el.style.height = Math.min(el.scrollHeight, 140) + 'px';
    }
};

// ==================== Module: UI Manager ====================
const UIManager = {
    showError(message) {
        DOM.errorMessage.textContent = message;
        DOM.errorMessage.classList.add('show');
        setTimeout(() => {
            DOM.errorMessage.classList.remove('show');
        }, 5000);
    },

    showSuccess(message) {
        DOM.successMessage.textContent = message;
        DOM.successMessage.classList.add('show');
        setTimeout(() => {
            DOM.successMessage.classList.remove('show');
        }, 3000);
    },

    setConnectionStatus(isConnected) {
        if (isConnected) {
            DOM.statusDot.style.background = '#4caf50';
            DOM.statusText.textContent = 'Connected';
        } else {
            DOM.statusDot.style.background = '#f44336';
            DOM.statusText.textContent = 'Disconnected';
        }
    },

    setTypingIndicator(show) {
        DOM.typingIndicator.classList.toggle('active', show);
        if (show) {
            DOM.chatContainer.scrollTop = DOM.chatContainer.scrollHeight;
        }
    },

    updateTimestamp() {
        const now = new Date();
        DOM.timestampElement.textContent = now.toLocaleTimeString();
    },

    showUploadProgress(show) {
        DOM.uploadProgress.classList.toggle('show', show);
    },

    updateProgress(percent, loaded, total) {
        DOM.progressBar.style.width = percent + '%';
        DOM.progressPercent.textContent = percent + '%';
        DOM.progressSize.textContent = `${Utils.formatFileSize(loaded)} / ${Utils.formatFileSize(total)}`;
    },

    showFilePreview(file) {
        DOM.filePreview.style.display = 'flex';
        DOM.filePreviewIcon.textContent = Utils.getFileIcon(file.name);
        DOM.filePreviewName.textContent = file.name;
        DOM.filePreviewSize.textContent = Utils.formatFileSize(file.size);
    },

    hideFilePreview() {
        DOM.filePreview.style.display = 'none';
    }
};

// ==================== Module: Message Manager ====================
const MessageManager = {
    addMessage(text, isUser, fileInfo = null) {
        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${isUser ? 'user' : 'assistant'}`;

        const bubbleDiv = document.createElement('div');
        bubbleDiv.className = 'message-bubble';

        if (fileInfo) {
            const fileDiv = this.createFileElement(fileInfo);
            bubbleDiv.appendChild(fileDiv);
        }

        if (text) {
            bubbleDiv.appendChild(document.createTextNode(text));
        }

        const timeDiv = document.createElement('div');
        timeDiv.className = 'message-time';
        timeDiv.textContent = Utils.getCurrentTime();

        bubbleDiv.appendChild(timeDiv);
        messageDiv.appendChild(bubbleDiv);

        DOM.chatContainer.appendChild(messageDiv);
        DOM.chatContainer.scrollTop = DOM.chatContainer.scrollHeight;

        AppState.addMessage(text, isUser, fileInfo);
    },

    createFileElement(fileInfo) {
        const fileDiv = document.createElement('div');
        fileDiv.className = 'message-file';

        const iconSpan = document.createElement('span');
        iconSpan.className = 'file-icon';
        iconSpan.textContent = Utils.getFileIcon(fileInfo.name);

        const infoDiv = document.createElement('div');
        infoDiv.className = 'file-info';

        const nameDiv = document.createElement('div');
        nameDiv.className = 'file-name';
        nameDiv.textContent = fileInfo.name;

        const sizeDiv = document.createElement('div');
        sizeDiv.className = 'file-size';
        sizeDiv.textContent = fileInfo.size;

        infoDiv.appendChild(nameDiv);
        infoDiv.appendChild(sizeDiv);

        fileDiv.appendChild(iconSpan);
        fileDiv.appendChild(infoDiv);

        return fileDiv;
    }
};

// ==================== Module: API Manager ====================
const APIManager = {
    async checkStatus() {
        try {
            const response = await fetch('/api/status');
            return response.ok;
        } catch {
            return false;
        }
    },

    async sendMessage(message) {
        const response = await fetch('/api/send', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message })
        });

        if (!response.ok) {
            throw new Error(`Server error: ${response.status}`);
        }

        return await response.json();
    },

    async uploadFile(file, onProgress) {
        const formData = new FormData();
        formData.append('file', file);

        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();

            xhr.upload.addEventListener('progress', (e) => {
                if (e.lengthComputable && onProgress) {
                    const percent = Math.round((e.loaded / e.total) * 100);
                    onProgress(percent, e.loaded, e.total);
                }
            });

            xhr.addEventListener('load', () => {
                if (xhr.status === 200) {
                    try {
                        const response = JSON.parse(xhr.responseText);
                        resolve(response);
                    } catch {
                        reject(new Error('Invalid response from server'));
                    }
                } else {
                    reject(new Error(`Upload failed: ${xhr.status}`));
                }
            });

            xhr.addEventListener('error', () => reject(new Error('Network error during upload')));
            xhr.addEventListener('abort', () => reject(new Error('Upload cancelled')));

            AppState.setCurrentUpload(xhr);

            xhr.open('POST', '/api/upload');
            xhr.send(formData);
        });
    }
};

// ==================== Module: Event Handlers ====================
const EventHandlers = {
    async handleSendMessage() {
        const message = DOM.messageInput.value.trim();

        if (!message || AppState.isProcessing) {
            UIHelpers.updateSendButtonState();
            return;
        }

        MessageManager.addMessage(message, true);

        DOM.messageInput.value = '';
        UIHelpers.autoResizeTextarea(DOM.messageInput);
        UIHelpers.updateSendButtonState();

        AppState.setProcessing(true);
        UIHelpers.updateSendButtonState();
        UIManager.setTypingIndicator(true);

        try {
            const data = await APIManager.sendMessage(message);

            if (data.success && data.response) {
                UIManager.setTypingIndicator(false);
                MessageManager.addMessage(data.response, false);
            } else {
                throw new Error(data.error || 'Unknown error');
            }
        } catch (error) {
            console.error('Error sending message:', error);
            UIManager.setTypingIndicator(false);
            UIManager.showError(`Failed to send message: ${error.message}`);
        } finally {
            AppState.setProcessing(false);
            UIHelpers.updateSendButtonState();
            DOM.messageInput.focus();
        }
    },

    handleUploadClick() {
        if (AppState.isProcessing) return;
        DOM.fileInput.click();
    },

    async handleFileSelect(event) {
        const file = event.target.files[0];
        if (!file) return;

        const maxSize = 100 * 1024 * 1024;
        if (file.size > maxSize) {
            UIManager.showError('File size exceeds 100MB limit');
            DOM.fileInput.value = '';
            return;
        }

        UIManager.showUploadProgress(true);
        UIManager.showFilePreview(file);
        UIManager.updateProgress(0, 0, file.size);

        AppState.setProcessing(true);
        DOM.uploadButton.disabled = true;
        UIHelpers.updateSendButtonState();

        try {
            const result = await APIManager.uploadFile(file, (percent, loaded, total) => {
                UIManager.updateProgress(percent, loaded, total);
            });

            if (result.success && result.files && result.files.length > 0) {
                const uploadedFile = result.files[0];

                if (uploadedFile.success) {
                    MessageManager.addMessage(
                        'File uploaded successfully!',
                        true,
                        {
                            name: uploadedFile.fileName,
                            size: uploadedFile.sizeFormatted
                        }
                    );
                    UIManager.showSuccess(`${uploadedFile.fileName} uploaded successfully!`);
                } else {
                    throw new Error(uploadedFile.error || 'Upload failed');
                }
            } else {
                throw new Error('No file information received');
            }
        } catch (error) {
            console.error('Upload error:', error);
            UIManager.showError(`Upload failed: ${error.message}`);
        } finally {
            UIManager.showUploadProgress(false);
            UIManager.hideFilePreview();
            AppState.setProcessing(false);
            AppState.setCurrentUpload(null);

            DOM.uploadButton.disabled = false;
            UIHelpers.updateSendButtonState();
            DOM.fileInput.value = '';
        }
    },

    handleCancelUpload() {
        if (AppState.currentUpload) {
            AppState.currentUpload.abort();
            AppState.setCurrentUpload(null);
            UIManager.showUploadProgress(false);
            UIManager.hideFilePreview();

            AppState.setProcessing(false);
            DOM.uploadButton.disabled = false;
            UIHelpers.updateSendButtonState();
            DOM.fileInput.value = '';
        }
    },

    async checkServerStatus() {
        const isConnected = await APIManager.checkStatus();
        UIManager.setConnectionStatus(isConnected);
    }
};

// ==================== Module: App Initialization ====================
const App = {
    init() {
        UIManager.updateTimestamp();
        setInterval(() => UIManager.updateTimestamp(), 1000);

        MessageManager.addMessage(
            'Hello! I\'m Xiaowei Assistant. How can I help you today? 👋',
            false
        );

        this.setupEventListeners();

        EventHandlers.checkServerStatus();
        setInterval(() => EventHandlers.checkServerStatus(), 5000);

        UIHelpers.autoResizeTextarea(DOM.messageInput);
        UIHelpers.updateSendButtonState();
    },

    setupEventListeners() {
        DOM.sendButton.addEventListener('click', () => EventHandlers.handleSendMessage());
        DOM.uploadButton.addEventListener('click', () => EventHandlers.handleUploadClick());
        DOM.fileInput.addEventListener('change', (e) => EventHandlers.handleFileSelect(e));
        DOM.cancelUpload.addEventListener('click', () => EventHandlers.handleCancelUpload());

        // ChatGPT-like: input auto grow + send button enable/disable
        DOM.messageInput.addEventListener('input', () => {
            UIHelpers.autoResizeTextarea(DOM.messageInput);
            UIHelpers.updateSendButtonState();
        });

        // ChatGPT-like: Enter send, Shift+Enter newline
        DOM.messageInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                if (e.shiftKey) return;
                e.preventDefault();
                if (!AppState.isProcessing) EventHandlers.handleSendMessage();
            }
        });
    }
};

// ==================== Start Application ====================
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => App.init());
} else {
    App.init();
}
