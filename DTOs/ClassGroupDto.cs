namespace Messenger.DTOs;

public class ClassGroupDto
{
    public long ClassId { get; set; }
    public string? LevelName { get; set; }
    public long TeacherUserId { get; set; }
    public string? ClassTiming { get; set; }
    public bool IsActive { get; set; }
    public int LeftSes { get; set; } // Assuming 'LeftSes' means 'Left Sessions'
    public DateTime EndDate { get; set; }
    public double LastReadMessageId { get; set; } = 0;

    // برای نمایش مشخصات اخرین پیام
    public ChatMessageDto? LastMessage { get; set; } = null;
    public int UnreadCount { get; set; }
    // public UserDto TeacherUser { get; set; }
    // public ICollection<UserClassGroupDto> UserClassGroups { get; set; }
    // public ICollection<ClassGroupMessageDto> ClassGroupMessages { get; set; }
}
