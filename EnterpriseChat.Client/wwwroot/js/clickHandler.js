// clickHandler.js 

let currentHandler = null;

export function registerClickHandler(dotNetHelper) {
    // 1. تنظيف أي مستمع قديم عشان م يحصلش تكرار (Memory Leak)
    unregisterClickHandler();

    currentHandler = async (event) => {
        // فحص ذكي: لو الضغطة حصلت جوا قائمة مفتوحة أصلاً، م تعملش حاجة
        // ده بيمنع إن القائمة تقفل نفسها لما تضغط على خيار جواها
        if (event.target.closest('.msg-context-menu') || event.target.closest('.reactions-hover-wrap')) {
            return;
        }

        try {
            // بننادي دالة C# اللي في الـ Razor
            await dotNetHelper.invokeMethodAsync('OnDocumentClick');
        } catch (error) {
            // لو الـ Component اتشال من الشاشة، بنظف الـ Handler
            unregisterClickHandler();
        }
    };

    // 2. التعديل الجوهري: شيلنا الـ 'true' وخليناها 'false' (الوضع الافتراضي)
    // ده بيسمح لـ stopPropagation اللي في الـ Razor إنها تشتغل صح
    document.addEventListener('click', currentHandler, false);

    return currentHandler;
}

export function unregisterClickHandler() {
    if (currentHandler) {
        document.removeEventListener('click', currentHandler, false);
        currentHandler = null;
    }
}

// السطور دي عشان الـ Blazor يشوف الدوال دي من أي مكان
window.registerClickHandler = registerClickHandler;
window.unregisterClickHandler = unregisterClickHandler;