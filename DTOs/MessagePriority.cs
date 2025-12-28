namespace Messenger.DTOs
{
    /// <summary>
    /// اولویت پردازش پیام در صف
    /// </summary>
    public enum MessagePriority
    {
        /// <summary>
        /// اولویت پایین
        /// </summary>
        Low = -1,

        /// <summary>
        /// اولویت عادی (پیشفرض)
        /// </summary>
        Normal = 0,

        /// <summary>
        /// اولویت بالا
        /// </summary>
        High = 1,

        /// <summary>
        /// اولویت بحرانی
        /// </summary>
        Critical = 2
    }
}
