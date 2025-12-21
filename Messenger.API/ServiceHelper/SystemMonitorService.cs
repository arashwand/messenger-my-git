using Messenger.API.ServiceHelper.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace Messenger.API.ServiceHelper
{
    /// <summary>
    /// پیادهسازی سرویس مانیتورینگ سیستم برای Load Balancing
    /// از Performance Counters و Redis برای سنجش فشار سیستم استفاده میکند
    /// </summary>
    public class SystemMonitorService : ISystemMonitorService
    {
        private readonly IRedisUserStatusService _redisUserStatus;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SystemMonitorService> _logger;
        private readonly Process _currentProcess;

        // تنظیمات Threshold
        private const double CpuThreshold = 70.0; // درصد
        private const double MemoryThreshold = 75.0; // درصد
        private const int ActiveConnectionsThreshold = 500; // تعداد

        // Cache keys and durations
        private const string CpuCacheKey = "SystemMonitor:CpuUsage";
        private const string MemoryCacheKey = "SystemMonitor:MemoryUsage";
        private const int CacheExpirationSeconds = 5; // Cache برای 5 ثانیه

        public SystemMonitorService(
            IRedisUserStatusService redisUserStatus,
            IMemoryCache cache,
            ILogger<SystemMonitorService> logger)
        {
            _redisUserStatus = redisUserStatus;
            _cache = cache;
            _logger = logger;
            _currentProcess = Process.GetCurrentProcess();
        }

        /// <summary>
        /// دریافت درصد استفاده از CPU (با cache برای کاهش overhead)
        /// از آخرین مقدار cache استفاده میکند یا مقدار پیشفرض را برمیگرداند
        /// </summary>
        public async Task<double> GetCpuUsageAsync()
        {
            try
            {
                // بررسی cache
                if (_cache.TryGetValue(CpuCacheKey, out double cachedValue))
                {
                    return cachedValue;
                }

                // اگر cache وجود ندارد، یک مقدار تخمینی بر اساس Process info برمیگردانیم
                // بدون delay برای جلوگیری از latency در تصمیمگیری
                _currentProcess.Refresh();
                var cpuTime = _currentProcess.TotalProcessorTime.TotalMilliseconds;
                var processUptime = (DateTime.UtcNow - _currentProcess.StartTime).TotalMilliseconds;
                
                if (processUptime > 0)
                {
                    var cpuUsagePercentage = (cpuTime / (Environment.ProcessorCount * processUptime)) * 100;
                    
                    // ذخیره در cache
                    _cache.Set(CpuCacheKey, cpuUsagePercentage, TimeSpan.FromSeconds(CacheExpirationSeconds));
                    
                    _logger.LogDebug("CPU Usage (estimated): {CpuUsage:F2}%", cpuUsagePercentage);
                    return Math.Round(cpuUsagePercentage, 2);
                }

                return await Task.FromResult(0.0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting CPU usage");
                return 0;
            }
        }

        /// <summary>
        /// دریافت درصد استفاده از حافظه
        /// </summary>
        public Task<double> GetMemoryUsageAsync()
        {
            try
            {
                // بررسی cache
                if (_cache.TryGetValue(MemoryCacheKey, out double cachedValue))
                {
                    return Task.FromResult(cachedValue);
                }

                _currentProcess.Refresh();
                var workingSetMemory = _currentProcess.WorkingSet64;
                var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

                var memoryUsagePercentage = (workingSetMemory / (double)totalMemory) * 100;

                // ذخیره در cache
                _cache.Set(MemoryCacheKey, memoryUsagePercentage, TimeSpan.FromSeconds(CacheExpirationSeconds));

                _logger.LogDebug("Memory Usage: {MemoryUsage:F2}%", memoryUsagePercentage);
                return Task.FromResult(Math.Round(memoryUsagePercentage, 2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting memory usage");
                return Task.FromResult(0.0);
            }
        }

        /// <summary>
        /// دریافت تعداد کاربران آنلاین از Redis
        /// </summary>
        public async Task<int> GetActiveConnectionsAsync()
        {
            try
            {
                // از Redis برای شمارش کاربران آنلاین استفاده میکنیم
                // این تعداد واقعی کاربران متصل به SignalR است
                var onlineUsersCount = await _redisUserStatus.GetTotalOnlineUsersCountAsync();

                _logger.LogDebug("Active Connections: {Count}", onlineUsersCount);
                return onlineUsersCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active connections count");
                return 0;
            }
        }

        /// <summary>
        /// بررسی آیا سیستم تحت فشار است
        /// </summary>
        public async Task<bool> IsSystemUnderPressureAsync()
        {
            try
            {
                var cpuUsage = await GetCpuUsageAsync();
                var memoryUsage = await GetMemoryUsageAsync();
                var activeConnections = await GetActiveConnectionsAsync();

                var isUnderPressure = cpuUsage > CpuThreshold ||
                                     memoryUsage > MemoryThreshold ||
                                     activeConnections > ActiveConnectionsThreshold;

                if (isUnderPressure)
                {
                    _logger.LogWarning(
                        "System under pressure - CPU: {Cpu:F2}%, Memory: {Memory:F2}%, Connections: {Connections}",
                        cpuUsage, memoryUsage, activeConnections);
                }

                return isUnderPressure;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system pressure");
                return false; // در صورت خطا، فرض میکنیم سیستم تحت فشار نیست
            }
        }

        /// <summary>
        /// محاسبه امتیاز بار سیستم (0-1)
        /// وزندهی: CPU=40%, Memory=30%, Connections=30%
        /// </summary>
        public async Task<double> GetSystemLoadScoreAsync()
        {
            try
            {
                var cpuUsage = await GetCpuUsageAsync();
                var memoryUsage = await GetMemoryUsageAsync();
                var activeConnections = await GetActiveConnectionsAsync();

                // نرمالسازی مقادیر به بازه 0-1
                var cpuScore = Math.Min(cpuUsage / 100.0, 1.0);
                var memoryScore = Math.Min(memoryUsage / 100.0, 1.0);
                var connectionsScore = Math.Min(activeConnections / (double)ActiveConnectionsThreshold, 1.0);

                // محاسبه امتیاز وزندار
                var loadScore = (cpuScore * 0.4) + (memoryScore * 0.3) + (connectionsScore * 0.3);

                _logger.LogDebug("System Load Score: {Score:F2} (CPU: {Cpu:F2}%, Memory: {Memory:F2}%, Connections: {Connections})",
                    loadScore, cpuUsage, memoryUsage, activeConnections);

                return Math.Round(loadScore, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating system load score");
                return 0;
            }
        }
    }
}
