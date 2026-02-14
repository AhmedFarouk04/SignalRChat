using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EnterpriseChat.Client.Services.Ui;

public class MenuStateService : INotifyPropertyChanged
{
    private Guid? _openMenuMessageId;

    public Guid? OpenMenuMessageId
    {
        get => _openMenuMessageId;
        set
        {
            if (_openMenuMessageId != value)
            {
                _openMenuMessageId = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ميثود مساعدة لفتح القائمة وإغلاق أي قائمة تانية تلقائياً
    public void SetOpenMenu(Guid? messageId)
    {
        OpenMenuMessageId = messageId;
    }

    public void CloseMenu() => OpenMenuMessageId = null;
}