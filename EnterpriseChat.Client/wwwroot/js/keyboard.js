let _handler = null;
let _dotnet = null;

export function registerEscape(dotnetRef) {
    _dotnet = dotnetRef;

    // لو كان في handler قديم، شيله
    if (_handler) window.removeEventListener("keydown", _handler);

    _handler = (e) => {
        if (e.key === "Escape") {
            // نادِ Blazor method
            _dotnet?.invokeMethodAsync("OnGlobalEscape");
        }
    };

    window.addEventListener("keydown", _handler);
}

export function unregisterEscape() {
    if (_handler) window.removeEventListener("keydown", _handler);
    _handler = null;
    _dotnet = null;
}
let _handler = null;
let _dotnet = null;

export function registerEscape(dotnetRef) {
    _dotnet = dotnetRef;

    // لو كان في handler قديم، شيله
    if (_handler) window.removeEventListener("keydown", _handler);

    _handler = (e) => {
        if (e.key === "Escape") {
            // نادِ Blazor method
            _dotnet?.invokeMethodAsync("OnGlobalEscape");
        }
    };

    window.addEventListener("keydown", _handler);
}

export function unregisterEscape() {
    if (_handler) window.removeEventListener("keydown", _handler);
    _handler = null;
    _dotnet = null;
}
