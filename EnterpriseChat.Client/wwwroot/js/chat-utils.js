// wwwroot/js/chat-utils.js

// ✅ دوال للردود
window.scrollToMessage = (elementId) => {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
};

window.highlightMessage = (elementId) => {
    const element = document.getElementById(elementId);
    if (element) {
        element.classList.add('highlighted');
        setTimeout(() => {
            element.classList.remove('highlighted');
        }, 2000);
    }
};

// ✅ دالة focus محسنة
window.focusElement = (elementOrId) => {
    try {
        const element = typeof elementOrId === "string"
            ? document.getElementById(elementOrId) || document.querySelector(elementOrId)
            : elementOrId;

        if (element && element.focus) {
            element.focus();
        }
    } catch (error) {
        console.warn("Could not focus element:", error);
    }
};

// ✅ دالة لإزالة الـ highlight
window.clearHighlights = () => {
    document.querySelectorAll('.highlighted').forEach(el => {
        el.classList.remove('highlighted');
    });
};

// ✅ دالة لإنشاء عنصر رد مؤقت
window.createReplyIndicator = (messageId, replyId) => {
    const messageEl = document.getElementById(`message-${messageId}`);
    if (messageEl) {
        const indicator = document.createElement('div');
        indicator.className = 'reply-indicator';
        indicator.innerHTML = `↩️ Replied`;
        indicator.style.cssText = `
            position: absolute;
            top: -10px;
            right: 10px;
            background: rgba(56, 189, 248, 0.9);
            color: white;
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 11px;
            z-index: 10;
        `;
        messageEl.style.position = 'relative';
        messageEl.appendChild(indicator);
        
        // إزالة المؤقت بعد 3 ثواني
        setTimeout(() => {
            if (indicator.parentNode) {
                indicator.parentNode.removeChild(indicator);
            }
        }, 3000);
    }
};