export function scrollToBottom(el) {
    el.scrollTop = el.scrollHeight;
}

export function isAtBottom(el) {
    return el.scrollHeight - el.scrollTop - el.clientHeight < 5;
}