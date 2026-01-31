using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EnterpriseChat.Client.Services.JsInterop;

public sealed class ScrollService : IScrollService, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module;

    public ScrollService(IJSRuntime js)
    {
        _module = new(() => js.InvokeAsync<IJSObjectReference>(
            "import", "./js/scroll.js").AsTask());
    }

    public async Task ScrollToBottomAsync(ElementReference el)
    {
        var m = await _module.Value;
        await m.InvokeVoidAsync("scrollToBottom", el);
    }

    public async Task ScrollToBottomSmoothAsync(ElementReference el)
    {
        var m = await _module.Value;
        await m.InvokeVoidAsync("scrollToBottomSmooth", el);
    }

    private static int _atBottomCalls;
    private static DateTime _atBottomLog = DateTime.UtcNow;

    public async Task<bool> IsAtBottomAsync(ElementReference el)
    {
        _atBottomCalls++;
        var now = DateTime.UtcNow;
        if ((now - _atBottomLog).TotalSeconds >= 2)
        {
            Console.WriteLine($"[JS IsAtBottom] last2s={_atBottomCalls}");
            _atBottomCalls = 0;
            _atBottomLog = now;
        }

        var m = await _module.Value;
        return await m.InvokeAsync<bool>("isAtBottom", el);
    }


    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            var m = await _module.Value;
            await m.DisposeAsync();
        }
    }
}
