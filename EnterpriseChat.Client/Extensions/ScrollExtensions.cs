using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EnterpriseChat.Client.Extensions;

public static class ScrollExtensions
{
    public static ValueTask ScrollToBottomAsync(
        this ElementReference element)
        => JS.InvokeVoidAsync("scrollHelper.scrollToBottom", element);

    public static ValueTask<bool> IsScrolledToBottomAsync(
        this ElementReference element)
        => JS.InvokeAsync<bool>("scrollHelper.isAtBottom", element);

    private static IJSRuntime JS => _js!;
    private static IJSRuntime? _js;

    public static void Init(IJSRuntime js)
    {
        _js = js;
    }
}
