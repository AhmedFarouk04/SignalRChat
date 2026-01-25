export function scrollToBottom(el) {
    el.scrollTop = el.scrollHeight;
}

export function scrollToBottomSmooth(el) {
    // Smooth فقط لما المستخدم يضغط زر ↓ New (مش auto)
    el.scrollTo({ top: el.scrollHeight, behavior: "smooth" });
}

export function isAtBottom(el) {
    return el.scrollHeight - el.scrollTop - el.clientHeight < 5;
}

// "/" أو Ctrl+K يركز على search input (لو موجود)
export function registerRoomsSearchShortcuts(inputId) {
    if (window.__roomsSearchShortcutsRegistered) return;
    window.__roomsSearchShortcutsRegistered = true;

    window.addEventListener("keydown", (e) => {
        // ignore داخل inputs/textarea/contenteditable
        const t = e.target;
        const tag = (t && t.tagName) ? t.tagName.toLowerCase() : "";
        const isEditable =
            tag === "input" ||
            tag === "textarea" ||
            (t && t.isContentEditable);

        // "/" (بدون modifiers)
        const slash = e.key === "/" && !e.ctrlKey && !e.metaKey && !e.altKey;

        // Ctrl+K / Cmd+K
        const ctrlK =
            (e.key === "k" || e.key === "K") && (e.ctrlKey || e.metaKey);

        if (isEditable) return;
        if (!slash && !ctrlK) return;

        const input = document.getElementById(inputId);
        if (!input) return;

        e.preventDefault();
        input.focus();
        // select existing text for fast replace
        try {
            input.select?.();
        } catch { }
    });
}
