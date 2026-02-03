let escapeHandler = null;

export function registerEscape(dotNetRef) {
    if (escapeHandler) return;

    escapeHandler = (e) => {
        if (e.key === "Escape") {
            try {
                dotNetRef.invokeMethodAsync("OnGlobalEscape");
            } catch { }
        }
    };

    window.addEventListener("keydown", escapeHandler);
}

export function unregisterEscape() {
    if (!escapeHandler) return;
    window.removeEventListener("keydown", escapeHandler);
    escapeHandler = null;
}
window.scrollToBottom = (idOrEl) => {
    const el =
        (typeof idOrEl === "string")
            ? document.getElementById(idOrEl) || document.querySelector("." + idOrEl)
            : idOrEl;

    if (!el) return;
    el.scrollTop = el.scrollHeight;
};
