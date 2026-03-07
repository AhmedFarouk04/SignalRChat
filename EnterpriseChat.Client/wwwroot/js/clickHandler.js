let currentHandler = null;

export function registerClickHandler(dotNetHelper) {
    if (currentHandler) {
        document.removeEventListener('click', currentHandler, false); // ✅ false
    }
    currentHandler = async (event) => {
        const isIgnored = event.target.closest('.reactions-hover-wrap') ||
            event.target.closest('.reaction-btn') ||
            event.target.closest('.reaction-actions') ||  // ✅ أضف ده
            event.target.closest('.msg-context-menu') ||
            event.target.closest('.msg-options-btn') ||
            event.target.closest('.quick-react-btn') ||
            event.target.closest('.reaction-modal') ||
            event.target.closest('.ReactionsPanel') ||    // ✅ أضف ده
            event.target.closest('[class*="reaction"]');  // ✅ أضف ده - يشمل أي class فيه reaction
        if (isIgnored) return;
        try {
            await dotNetHelper.invokeMethodAsync('OnDocumentClick');
        } catch (error) {
            console.error("Error invoking OnDocumentClick", error);
        }
    };
    document.addEventListener('click', currentHandler, false);
}

export function unregisterClickHandler() {
    if (currentHandler) {
        document.removeEventListener('click', currentHandler, false); // ✅ false
        currentHandler = null;
    }
}