using Microsoft.JSInterop;

public sealed class NotificationSoundService
{
    private readonly IJSRuntime _js;
    private bool _isInitialized = false;
    private bool _isUnlocked = false;

    public NotificationSoundService(IJSRuntime js) => _js = js;

    public async Task<bool> InitializeAsync(string soundUrl = "/sounds/notify.mp3")
    {
        try
        {
            if (!_isInitialized)
            {
                Console.WriteLine($"[NotificationSound] Initializing with URL: {soundUrl}");
                await _js.InvokeVoidAsync("initNotifySound", soundUrl);
                _isInitialized = true;
                Console.WriteLine("[NotificationSound] Initialized");
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotificationSound] Initialize failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UnlockAsync()
    {
        try
        {
            Console.WriteLine("[NotificationSound] UnlockAsync called");

            if (!_isInitialized)
                await InitializeAsync();

            // ✅ استخدم notifyUnlock مش unlockNotify
            var result = await _js.InvokeAsync<bool>("notifyUnlock");
            _isUnlocked = result;

            Console.WriteLine($"[NotificationSound] Unlock result: {result}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotificationSound] Unlock failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PlayAsync()
    {
        try
        {
            Console.WriteLine("[NotificationSound] PlayAsync called");

            if (!_isUnlocked)
            {
                Console.WriteLine("[NotificationSound] Not unlocked, trying to unlock...");
                await UnlockAsync();
            }

            if (_isUnlocked)
            {
                // ✅ استخدم notifyPlay مش playNotify
                var result = await _js.InvokeAsync<bool>("notifyPlay");
                Console.WriteLine($"[NotificationSound] Play result: {result}");
                return result;
            }

            Console.WriteLine("[NotificationSound] Still locked after unlock attempt");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotificationSound] Play failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TestPlayAsync()
    {
        try
        {
            Console.WriteLine("[NotificationSound] TEST: Trying to play");
            return await PlayAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotificationSound] TEST failed: {ex.Message}");
            return false;
        }
    }

    public async Task ResetAsync()
    {
        try
        {
            _isUnlocked = false;
            await _js.InvokeVoidAsync("resetNotify");
            Console.WriteLine("[NotificationSound] Reset completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotificationSound] Reset failed: {ex.Message}");
        }
    }
}