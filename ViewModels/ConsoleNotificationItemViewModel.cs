using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.ViewModels;

public sealed partial class ConsoleNotificationItemViewModel : ObservableObject
{
    private readonly Action<uint> _reply;

    public ConsoleNotificationItemViewModel(
        DiscordNotificationMessage source,
        string timeLabel,
        bool isSelected,
        Action<uint> reply)
    {
        Source = source;
        TimeLabel = timeLabel;
        _isSelected = isSelected;
        _reply = reply;
    }

    public DiscordNotificationMessage Source { get; }

    public uint Id => Source.Id;

    public string Sender => Source.Sender;

    public string Body => Source.Body;

    public string TimeLabel { get; }

    public string ReplyDraft => Source.ReplyDraft ?? string.Empty;

    public bool HasReplyDraft => !string.IsNullOrWhiteSpace(Source.ReplyDraft);

    [ObservableProperty]
    private bool _isSelected;

    [RelayCommand]
    private void Select() => _reply(Id);
}
