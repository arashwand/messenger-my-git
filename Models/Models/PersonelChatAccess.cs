using System;
using System.Collections.Generic;

namespace Messenger.Models.Models;

/// <summary>
/// نگهداری ایدی چت و پرسنل جهت اینکه پرسنل مورد نظر دسترسی به ارسال پیام در گروه و ارسال به افراد ان گروه را دارد یا خیر.
/// 
/// </summary>
public partial class PersonelChatAccess
{
    public long Id { get; set; }

    public long TargetId { get; set; }

    public long PersonelId { get; set; }

    /// <summary>
    /// مجاز برای ارسال پیام در گروه چت یا کانال
    /// 
    /// </summary>
    public bool AccessSendMessageInChat { get; set; }

    /// <summary>
    /// مجاز برای ارسال پیام به اعضای گروه چت یا کانال
    /// </summary>
    public bool AccessToStudentMessage { get; set; }

    /// <summary>
    /// ClassGroup = group
    /// ChannelGroup = channel
    /// 
    /// </summary>
    public string GroupType { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User Personel { get; set; } = null!;
}
