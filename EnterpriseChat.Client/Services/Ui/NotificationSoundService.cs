using Microsoft.JSInterop;

public sealed class NotificationSoundService
{
    private readonly IJSRuntime _js;

    public NotificationSoundService(IJSRuntime js) => _js = js;

    public async Task<bool> UnlockAsync()
    {
        try
        {
            return await _js.InvokeAsync<bool>("notifyUnlock");
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> PlayAsync()
    {
        try
        {
            return await _js.InvokeAsync<bool>("notifyPlay");
        }
        catch
        {
            return false;
        }
    }
}
