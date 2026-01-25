using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EnterpriseChat.Client.Services.JsInterop;

public sealed class ScrollService : IScrollService
{
    private readonly IJSRuntime _js;

    public ScrollService(IJSRuntime js)
    {
        _js = js;
    }

    public Task ScrollToBottomAsync(ElementReference el)
        => _js.InvokeVoidAsync("scrollToBottom", el).AsTask();

    public Task ScrollToBottomSmoothAsync(ElementReference el)
        => _js.InvokeVoidAsync("scrollToBottomSmooth", el).AsTask();

    public Task<bool> IsAtBottomAsync(ElementReference el)
        => _js.InvokeAsync<bool>("isAtBottom", el).AsTask();
}
