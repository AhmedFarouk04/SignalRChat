using Microsoft.AspNetCore.Components;

namespace EnterpriseChat.Client.Services.JsInterop;

public interface IScrollService
{
    ValueTask ScrollToBottomAsync(ElementReference element);
    ValueTask<bool> IsAtBottomAsync(ElementReference element);
}
