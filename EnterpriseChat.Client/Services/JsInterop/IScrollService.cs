using Microsoft.AspNetCore.Components;

namespace EnterpriseChat.Client.Services.JsInterop;

public interface IScrollService
{
    Task ScrollToBottomAsync(ElementReference el);
    Task ScrollToBottomSmoothAsync(ElementReference el);
    Task<bool> IsAtBottomAsync(ElementReference el);
}
