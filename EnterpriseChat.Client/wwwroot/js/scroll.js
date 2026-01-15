window.scrollHelper = {
    scrollToBottom: function (el) {
        el.scrollTop = el.scrollHeight;
    },
    isAtBottom: function (el) {
        return el.scrollHeight - el.scrollTop - el.clientHeight < 5;
    }
};
