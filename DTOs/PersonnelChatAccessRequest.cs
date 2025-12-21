namespace Messenger.DTOs;

public class PersonnelChatAccessRequest
{
    public long PersonelId { get; set; }
    public int TargetId { get; set; }
    public string GroupType { get; set; } = null!;
    public bool AccessSendMessageInChat { get; set; }
    public bool AccessToStudentMessage { get; set; }
}