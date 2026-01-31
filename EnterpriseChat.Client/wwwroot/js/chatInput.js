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
