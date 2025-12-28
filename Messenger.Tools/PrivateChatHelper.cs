namespace Messenger.Tools;

public static class PrivateChatHelper
{
    /// <summary>
    /// تولید کلید یکتای SignalR Group برای چت خصوصی بر اساس شناسه مکالمه.
    /// </summary>
    /// <param name="conversationId">شناسه مکالمه (ConversationId)</param>
    /// <returns>کلید به فرمت private_{conversationId}</returns>
    public static string GeneratePrivateChatGroupKey(long conversationId)
    {
        return $"private_{conversationId}";
    }
    
    /// <summary>
    /// استخراج شناسه مکالمه (ConversationId) از groupKey چت خصوصی.
    /// </summary>
    /// <param name="groupKey">کلید گروه به فرمت private_{conversationId}</param>
    /// <returns>شناسه مکالمه یا null در صورت خطا</returns>
    public static long? ParsePrivateChatConversationId(string groupKey)
    {
        if (string.IsNullOrEmpty(groupKey) || !groupKey.StartsWith("private_"))
            return null;
            
        var parts = groupKey.Split('_');
        if (parts.Length != 2) return null;
        
        if (long.TryParse(parts[1], out long conversationId))
        {
            return conversationId;
        }
        
        return null;
    }
    
    /// <summary>
    /// تولید کلید سیستمی برای یک کاربر
    /// </summary>
    public static string GenerateSystemChatGroupKey(long userId)
    {
        return $"systemchat_{userId}";
    }
    
    /// <summary>
    /// تولید کلید گروه نقش
    /// </summary>
    /// <param name="roleName">نام نقش کاربر</param>
    /// <returns>کلید گروه نقش یا null برای نقش‌های بدون گروه (مانند Mentor و SystemBot)</returns>
    public static string? GenerateRoleGroupKey(string roleName)
    {
        return roleName switch
        {
            ConstRoles.Student => "role_students",
            ConstRoles.Teacher => "role_teachers",
            ConstRoles.Personel => "role_personnel",
            ConstRoles.Manager => "role_managers",
            _ => null // سایر نقش‌ها (مانند Mentor و SystemBot) گروه ندارند
        };
    }
}
