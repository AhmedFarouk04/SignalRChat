let _handler = null;
let _dotnet = null;

export function registerEscape(dotnetRef) {
    _dotnet = dotnetRef;

    if (_handler) window.removeEventListener("keydown", _handler);

    _handler = (e) => {
        if (e.key !== "Escape") return;

        // ignore داخل inputs/textarea/contenteditable
        const t = e.target;
        const tag = (t && t.tagName) ? t.tagName.toLowerCase() : "";
        const isEditable =
            tag === "input" ||
            tag === "textarea" ||
            (t && t.isContentEditable);

        if (isEditable) return;

        e.preventDefault();
        _dotnet?.invokeMethodAsync("OnGlobalEscape");
    };

    window.addEventListener("keydown", _handler);
}

export function unregisterEscape() {
    if (_handler) window.removeEventListener("keydown", _handler);
    _handler = null;
    _dotnet = null;
}
