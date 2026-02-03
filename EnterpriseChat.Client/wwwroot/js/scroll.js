export function scrollToBottom(idOrEl) {
    const el = (typeof idOrEl === "string")
        ? document.getElementById(idOrEl)
        : idOrEl;

    if (!el) return;
    el.scrollTop = el.scrollHeight;
}

export function isAtBottom(idOrEl) {
    const el = (typeof idOrEl === "string")
        ? document.getElementById(idOrEl)
        : idOrEl;

    if (!el) return true;
    return el.scrollHeight - el.scrollTop - el.clientHeight < 5;
}

export function scrollToBottomSmooth(idOrEl) {
    const el = (typeof idOrEl === "string")
        ? document.getElementById(idOrEl)
        : idOrEl;

    if (!el) return;
    el.scrollTo({ top: el.scrollHeight, behavior: "smooth" });
}

export function registerRoomsSearchShortcuts(inputSelector = 'input[type="search"], .rooms-search input, #roomsSearch') {
    // الفكرة: Ctrl+K أو / يركز على صندوق البحث في Rooms
    const handler = (e) => {
        const isCtrlK = (e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K');
        const isSlash = e.key === '/' && !e.ctrlKey && !e.metaKey && !e.altKey;

        if (!isCtrlK && !isSlash) return;

        // ما نسرقش الاختصار لو المستخدم جوه input/textarea أصلاً
        const tag = (document.activeElement?.tagName || '').toLowerCase();
        if (tag === 'input' || tag === 'textarea') return;

        const el = document.querySelector(inputSelector);
        if (!el) return;

        e.preventDefault();
        el.focus();
        if (typeof el.select === 'function') el.select();
    };

    window.addEventListener('keydown', handler);

    // رجّع disposer علشان لو احتجنا نفكّه
    return () => window.removeEventListener('keydown', handler);
}
