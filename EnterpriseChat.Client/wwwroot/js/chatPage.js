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
// أضف هذا الكود في نهاية ملف chatPage.js
// chatPage.js
// chatPage.js
// chatPage.js

let clickOutsideHandlers = new Map(); // عشان ندير أكتر من handler لو لزم

export function registerClickOutside(dotNetRef, componentId) {
    const handler = (e) => {
        // نستثني كل العناصر اللي ممكن تكون جزء من الـ UI المنبثق
        const insideReaction = e.target.closest('.reactions-hover-wrap, .quick-react-btn');
        const insideMenu = e.target.closest('.msg-context-menu, .msg-options-btn');

        if (!insideReaction && !insideMenu) {
            dotNetRef.invokeMethodAsync("CloseAllPopups")
                .catch(err => console.log("CloseAllPopups failed:", err));
        }
    };

    // نستخدم capture phase عشان نضمن التقاط الحدث قبل stopPropagation
    document.addEventListener('click', handler, true);

    // نحفظ الـ handler مرتبط بـ componentId عشان نعرف نشيله بعدين
    clickOutsideHandlers.set(componentId, { handler, dotNetRef });

    return componentId; // أو أي identifier
}

export function unregisterClickOutside(componentId) {
    const entry = clickOutsideHandlers.get(componentId);
    if (entry) {
        document.removeEventListener('click', entry.handler, true);
        clickOutsideHandlers.delete(componentId);
    }
}