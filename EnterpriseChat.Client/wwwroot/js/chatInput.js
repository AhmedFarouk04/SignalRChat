export function insertNewline(el) {
    if (!el) return;
    const start = el.selectionStart ?? el.value.length;
    const end = el.selectionEnd ?? el.value.length;

    const v = el.value ?? "";
    el.value = v.substring(0, start) + "\n" + v.substring(end);

    const pos = start + 1;
    el.setSelectionRange(pos, pos);

    el.dispatchEvent(new Event("input", { bubbles: true }));
}

export function focusEl(el) {
    if (!el) return;
    el.focus();
}
