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

    public ValueTask ScrollToBottomAsync(ElementReference element)
        => _js.InvokeVoidAsync("scrollToBottom", element);

    public ValueTask<bool> IsAtBottomAsync(ElementReference element)
        => _js.InvokeAsync<bool>("isAtBottom", element);
}
