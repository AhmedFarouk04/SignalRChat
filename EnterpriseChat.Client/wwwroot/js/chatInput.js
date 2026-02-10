window.chatInsertNewline = function (id) {
    const el = document.getElementById(id);
    if (!el) return;
    const start = el.selectionStart;
    const end = el.selectionEnd;
    const value = el.value;
    el.value = value.substring(0, start) + "\n" + value.substring(end);
    el.selectionStart = el.selectionEnd = start + 1;
};
export function focusEl(selectorOrElement) {
    try {
        const el = typeof selectorOrElement === "string"
            ? document.querySelector(selectorOrElement)
            : selectorOrElement;

        if (el && el.focus) el.focus();
    } catch { }
}
window.chatFocus = function (id) {
    const el = document.getElementById(id);
    if (el) el.focus();
};

// في chatInput.js أضف:
window.fixTextareaHeight = function (textareaId) {
    const textarea = document.getElementById(textareaId);
    if (!textarea) return;

    // إعادة ضبط الـ height تلقائياً
    textarea.style.height = 'auto';
    textarea.style.height = Math.min(textarea.scrollHeight, 120) + 'px';

    // إزالة أي padding زائد
    textarea.style.padding = '4px 8px';
    textarea.style.lineHeight = '1.2';
};

// استدعاء تلقائي عند التركيز
window.autoFixComposer = function () {
    document.querySelectorAll('.composer-input').forEach(textarea => {
        textarea.addEventListener('focus', function () {
            this.style.padding = '4px 8px';
            this.style.lineHeight = '1.2';
        });

        textarea.addEventListener('input', function () {
            this.style.height = 'auto';
            this.style.height = Math.min(this.scrollHeight, 120) + 'px';
        });
    });
};
