namespace EnterpriseChat.Client.Services.Ui;

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record ToastItem(
    Guid Id,
    ToastType Type,
    string Title,
    string Message,
    int DurationMs
);

public sealed class ToastService
{
    public event Action<ToastItem>? OnShow;

    public void Show(string title, string message, ToastType type = ToastType.Info, int durationMs = 3500)
        => OnShow?.Invoke(new ToastItem(Guid.NewGuid(), type, title, message, durationMs));

    public void Info(string title, string message, int durationMs = 3000)
        => Show(title, message, ToastType.Info, durationMs);

    public void Success(string title, string message, int durationMs = 3000)
        => Show(title, message, ToastType.Success, durationMs);

    public void Warning(string title, string message, int durationMs = 4000)
        => Show(title, message, ToastType.Warning, durationMs);

    public void Error(string title, string message, int durationMs = 4500)
        => Show(title, message, ToastType.Error, durationMs);
}
