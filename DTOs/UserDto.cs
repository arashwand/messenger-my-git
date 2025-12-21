namespace Messenger.DTOs;

public class UserDto
{
    public long UserId { get; set; }
    public string? RoleName { get; set; }
    public string? RoleFaName { get; set; }
    public string? NameFamily { get; set; }

    /// <summary>
    /// Department Name
    /// </summary>
    public string? DeptName { get; set; }
    public string? ProfilePicName { get; set; }
    // LoginToken is likely handled by the authentication service, not usually exposed in a general UserDto
}

/// <summary>
/// پاسخ دریافتی از sso برای مشخصات یک کاربر
/// </summary>
public partial class ResponseUserinfoDto
{
    public long UserId { get; set; }

    public string RoleName { get; set; } = null!;

    public int BranchId { get; set; }

    public int PersonelId { get; set; }

    public int TeacherId { get; set; }

    public int StudentId { get; set; }

    public int MentorId { get; set; }

    public string? NameFamily { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? EnglishName { get; set; }

    public string? ProfileImageName { get; set; }

    public bool IsActive { get; set; }
}