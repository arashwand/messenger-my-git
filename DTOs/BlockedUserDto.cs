namespace Messenger.DTOs;

public class BlockedUserDto // Renamed from BlockedUsers for convention
{
    public long BlockedUserId { get; set; }
    public DateTime BlockDate { get; set; }
    public long UserId { get; set; } // The user who is blocked
    public string? Comment { get; set; }

    // It might be useful to know *who* blocked this user, but the diagram doesn't show this.
    // Assuming the context implies the current user blocked 'UserId'.
    // public UserDto User { get; set; }
}
