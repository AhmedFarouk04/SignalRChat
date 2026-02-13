using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EnterpriseChat.Client.Models;

public class MessageModel : INotifyPropertyChanged
{
    private Guid _id;
    public MessageStatus PersonalStatus { get; set; } = MessageStatus.Sent;  // default Sent
    public Guid Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }

    private Guid _roomId;
    public Guid RoomId
    {
        get => _roomId;
        set
        {
            if (_roomId != value)
            {
                _roomId = value;
                OnPropertyChanged();
            }
        }
    }

    private Guid _senderId;
    public Guid SenderId
    {
        get => _senderId;
        set
        {
            if (_senderId != value)
            {
                _senderId = value;
                OnPropertyChanged();
            }
        }
    }

    private string _content = string.Empty;
    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                OnPropertyChanged();
            }
        }
    }

    private MessageStatus _status = MessageStatus.Pending;
    public MessageStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    private DateTime _createdAt;
    public DateTime CreatedAt
    {
        get => _createdAt;
        set
        {
            if (_createdAt != value)
            {
                _createdAt = value;
                OnPropertyChanged();
            }
        }
    }

    private string? _error;
    public string? Error
    {
        get => _error;
        set
        {
            if (_error != value)
            {
                _error = value;
                OnPropertyChanged();
            }
        }
    }

    private List<MessageReceiptModel> _receipts = new();
    public List<MessageReceiptModel> Receipts
    {
        get => _receipts;
        set
        {
            if (_receipts != value)
            {
                _receipts = value;
                OnPropertyChanged();
            }
        }
    }

    private int _deliveredCount;
    public int DeliveredCount
    {
        get => _deliveredCount;
        set
        {
            if (_deliveredCount != value)
            {
                _deliveredCount = value;
                OnPropertyChanged();
            }
        }
    }

    private int _readCount;
    public int ReadCount
    {
        get => _readCount;
        set
        {
            if (_readCount != value)
            {
                _readCount = value;
                OnPropertyChanged();
            }
        }
    }

    private int _totalRecipients = 1;
    public int TotalRecipients
    {
        get => _totalRecipients;
        set
        {
            if (_totalRecipients != value)
            {
                _totalRecipients = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _hasReplies;
    public bool HasReplies
    {
        get => _hasReplies;
        set
        {
            if (_hasReplies != value)
            {
                _hasReplies = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isEdited;
    public bool IsEdited
    {
        get => _isEdited;
        set
        {
            if (_isEdited != value)
            {
                _isEdited = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isDeleted;
    public bool IsDeleted
    {
        get => _isDeleted;
        set
        {
            if (_isDeleted != value)
            {
                _isDeleted = value;
                OnPropertyChanged();
            }
        }
    }

    private DateTime? _updatedAt;
    public DateTime? UpdatedAt
    {
        get => _updatedAt;
        set
        {
            if (_updatedAt != value)
            {
                _updatedAt = value;
                OnPropertyChanged();
            }
        }
    }

    private DateTime? _deletedAt;
    public DateTime? DeletedAt
    {
        get => _deletedAt;
        set
        {
            if (_deletedAt != value)
            {
                _deletedAt = value;
                OnPropertyChanged();
            }
        }
    }

    private MessageReactionsModel? _reactions;
    public MessageReactionsModel? Reactions
    {
        get => _reactions;
        set
        {
            if (_reactions != value)
            {
                _reactions = value;
                OnPropertyChanged();
            }
        }
    }

    private Guid? _replyToMessageId;
    public Guid? ReplyToMessageId
    {
        get => _replyToMessageId;
        set
        {
            if (_replyToMessageId != value)
            {
                _replyToMessageId = value;
                OnPropertyChanged();
            }
        }
    }

    private ReplyInfoModel? _replyInfo;
    public ReplyInfoModel? ReplyInfo
    {
        get => _replyInfo;
        set
        {
            if (_replyInfo != value)
            {
                _replyInfo = value;
                OnPropertyChanged();
            }
        }
    }

    private string _type = "User";
    public string Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isSystem;
    public bool IsSystem
    {
        get => _isSystem;
        set
        {
            if (_isSystem != value)
            {
                _isSystem = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}