// clickHandler.js - ES Module + Global Fallback

let currentHandler = null;
let dotNetHelperRef = null;

export function registerClickHandler(dotNetHelper) {
    console.log('[clickHandler] Registering...');

    if (!dotNetHelper) {
        console.error('[clickHandler] No dotNetHelper provided');
        return null;
    }

    unregisterClickHandler(); // تنظيف

    dotNetHelperRef = dotNetHelper;

    currentHandler = async (event) => {
        if (!dotNetHelperRef) return;
        try {
            await dotNetHelperRef.invokeMethodAsync('OnDocumentClick');
        } catch (error) {
            if (error.message && (error.message.includes('disposed') || error.message.includes('no tracked object'))) {
                console.warn('[clickHandler] Auto-cleanup disposed reference');
                dotNetHelperRef = null;
                return;
            }
            console.error('[clickHandler] Error invoking method:', error);
        }
    };

    document.addEventListener('click', currentHandler, true);
    console.log('[clickHandler] Registered successfully');
    return currentHandler;
}

export function unregisterClickHandler() {
    console.log('[clickHandler] Unregistering...');

    if (currentHandler) {
        document.removeEventListener('click', currentHandler, true);
        currentHandler = null;
    }

    if (dotNetHelperRef) {
        try { dotNetHelperRef.dispose(); } catch (e) { }
        dotNetHelperRef = null;
    }
}

// Global fallback عشان أي مكان تاني بينادي window.
window.registerClickHandler = registerClickHandler;
window.unregisterClickHandler = unregisterClickHandler;