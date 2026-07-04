(function () {
    const supported = ['en', 'zh-CN'];
    const fallback = 'en';

    const state = {
        language: fallback,
        messages: {}
    };

    function pickLanguage() {
        const params = new URLSearchParams(window.location.search);
        const fromUrl = params.get('lang');
        const fromStorage = localStorage.getItem('lantransfer.lang');
        const fromBrowser = navigator.language;
        const candidates = [fromUrl, fromStorage, fromBrowser, fallback].filter(Boolean);

        for (const candidate of candidates) {
            if (supported.includes(candidate)) {
                return candidate;
            }

            if (candidate && candidate.toLowerCase().startsWith('zh')) {
                return 'zh-CN';
            }
        }

        return fallback;
    }

    async function loadMessages(language) {
        const response = await fetch(`/i18n/${language}.json`);
        if (!response.ok) {
            throw new Error(`Missing language: ${language}`);
        }

        return response.json();
    }

    function getValue(key) {
        return key.split('.').reduce((current, part) => current && current[part], state.messages);
    }

    function format(value, params) {
        if (!params) {
            return value;
        }

        return Object.entries(params).reduce(
            (text, [key, replacement]) => text.replaceAll(`{${key}}`, replacement),
            value);
    }

    function t(key, params) {
        return format(getValue(key) || key, params);
    }

    function applyTranslations() {
        document.documentElement.lang = state.language;

        document.querySelectorAll('[data-i18n]').forEach(element => {
            element.textContent = t(element.dataset.i18n);
        });

        document.querySelectorAll('[data-i18n-placeholder]').forEach(element => {
            element.placeholder = t(element.dataset.i18nPlaceholder);
        });

        document.querySelectorAll('[data-i18n-title]').forEach(element => {
            element.title = t(element.dataset.i18nTitle);
            element.setAttribute('aria-label', t(element.dataset.i18nTitle));
        });
    }

    async function setLanguage(language) {
        const next = supported.includes(language) ? language : fallback;
        state.messages = await loadMessages(next);
        state.language = next;
        localStorage.setItem('lantransfer.lang', next);
        applyTranslations();
    }

    async function init() {
        await setLanguage(pickLanguage());
    }

    window.lanTransferI18n = {
        init,
        setLanguage,
        t,
        get language() {
            return state.language;
        }
    };
})();
