namespace Messenger.API.ServiceHelper.Interfaces
{
    /// <summary>
    /// سرویس مانیتورینگ وضعیت سیستم برای Load Balancing
    /// </summary>
    public interface ISystemMonitorService
    {
        /// <summary>
        /// دریافت درصد استفاده از CPU
        /// </summary>
        /// <returns>درصد استفاده (0-100)</returns>
        Task<double> GetCpuUsageAsync();

        /// <summary>
        /// دریافت درصد استفاده از حافظه
        /// </summary>
        /// <returns>درصد استفاده (0-100)</returns>
        Task<double> GetMemoryUsageAsync();

        /// <summary>
        /// دریافت تعداد کاربران آنلاین فعلی
        /// </summary>
        /// <returns>تعداد کاربران آنلاین</returns>
        Task<int> GetActiveConnectionsAsync();

        /// <summary>
        /// بررسی آیا سیستم تحت فشار است
        /// </summary>
        /// <returns>true اگر سیستم تحت فشار باشد</returns>
        Task<bool> IsSystemUnderPressureAsync();

        /// <summary>
        /// دریافت امتیاز بار سیستم (0-1)
        /// </summary>
        /// <returns>امتیاز بار (0=بدون فشار, 1=فشار حداکثر)</returns>
        Task<double> GetSystemLoadScoreAsync();
    }
}
