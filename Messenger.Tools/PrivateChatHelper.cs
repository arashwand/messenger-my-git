namespace Messenger.Tools;

public static class PrivateChatHelper
{
    /// <summary>
    /// تولید کلید یکتای SignalR Group برای چت خصوصی بین دو کاربر
    /// همیشه کوچکترین userId اول قرار میگیرد
    /// </summary>
    /// <param name="userId1">شناسه کاربر اول</param>
    /// <param name="userId2">شناسه کاربر دوم</param>
    /// <returns>کلید به فرمت private_{min}_{max}</returns>
    public static string GeneratePrivateChatGroupKey(long userId1, long userId2)
    {
        var minId = Math.Min(userId1, userId2);
        var maxId = Math.Max(userId1, userId2);
        return $"private_{minId}_{maxId}";
    }
    
    /// <summary>
    /// استخراج دو userId از groupKey
    /// </summary>
    /// <param name="groupKey">کلید گروه به فرمت private_{id1}_{id2}</param>
    /// <returns>Tuple شامل دو userId یا null در صورت خطا</returns>
    public static (long userId1, long userId2)? ParsePrivateChatGroupKey(string groupKey)
    {
        if (string.IsNullOrEmpty(groupKey) || !groupKey.StartsWith("private_"))
            return null;
            
        var parts = groupKey.Split('_');
        if (parts.Length != 3) return null;
        
        if (long.TryParse(parts[1], out long id1) && long.TryParse(parts[2], out long id2))
        {
            return (id1, id2);
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
    public static string GenerateRoleGroupKey(string roleName)
    {
        return roleName switch
        {
            ConstRoles.Student => "role_students",
            ConstRoles.Teacher => "role_teachers",
            ConstRoles.Personel => "role_personnel",
            ConstRoles.Manager => "role_managers",
            _ => null
        };
    }
}
