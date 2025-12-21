using Messenger.API.ServiceHelper.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Messenger.Tools;
using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace Messenger.API.Pages
{
    /// <summary>
    /// داشبورد نمایش وضعیت Load Balancing و صف پیامها
    /// فقط برای Manager قابل دسترسی است
    /// </summary>
    [Authorize(Roles = ConstRoles.Manager)]
    public class LoadBalancingDashboardModel : PageModel
    {
        private readonly ISystemMonitorService _systemMonitor;
        private readonly ILogger<LoadBalancingDashboardModel> _logger;

        public LoadBalancingDashboardModel(
            ISystemMonitorService systemMonitor,
            ILogger<LoadBalancingDashboardModel> logger)
        {
            _systemMonitor = systemMonitor;
            _logger = logger;
        }

        // System Metrics
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public int ActiveConnections { get; set; }
        public double SystemLoadScore { get; set; }
        public bool IsSystemUnderPressure { get; set; }

        // Queue Statistics
        public long EnqueuedCount { get; set; }
        public long ProcessingCount { get; set; }
        public long SucceededCount { get; set; }
        public long FailedCount { get; set; }
        public long ScheduledCount { get; set; }
        public long DeletedCount { get; set; }

        // Queue Details by Priority
        public Dictionary<string, long> QueueCounts { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                // Load System Metrics
                CpuUsage = await _systemMonitor.GetCpuUsageAsync();
                MemoryUsage = await _systemMonitor.GetMemoryUsageAsync();
                ActiveConnections = await _systemMonitor.GetActiveConnectionsAsync();
                SystemLoadScore = await _systemMonitor.GetSystemLoadScoreAsync();
                IsSystemUnderPressure = await _systemMonitor.IsSystemUnderPressureAsync();

                // Load Hangfire Queue Statistics
                LoadQueueStatistics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
            }
        }

        /// <summary>
        /// Load statistics from Hangfire
        /// </summary>
        private void LoadQueueStatistics()
        {
            try
            {
                var monitor = JobStorage.Current.GetMonitoringApi();
                var statistics = JobStorage.Current.GetConnection().GetRecurringJobs();

                // Get overall statistics
                var stats = monitor.GetStatistics();
                EnqueuedCount = stats.Enqueued;
                ProcessingCount = stats.Processing;
                SucceededCount = stats.Succeeded;
                FailedCount = stats.Failed;
                ScheduledCount = stats.Scheduled;
                DeletedCount = stats.Deleted;

                // Get queue counts by priority
                var queues = new[] { "critical", "high", "default", "low" };
                foreach (var queue in queues)
                {
                    try
                    {
                        var queueJobs = monitor.EnqueuedJobs(queue, 0, 1);
                        QueueCounts[queue] = queueJobs.Count;
                    }
                    catch
                    {
                        QueueCounts[queue] = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Hangfire statistics");
            }
        }

        /// <summary>
        /// API endpoint to get real-time metrics (for AJAX refresh)
        /// </summary>
        public async Task<IActionResult> OnGetMetricsAsync()
        {
            try
            {
                var metrics = new
                {
                    cpuUsage = await _systemMonitor.GetCpuUsageAsync(),
                    memoryUsage = await _systemMonitor.GetMemoryUsageAsync(),
                    activeConnections = await _systemMonitor.GetActiveConnectionsAsync(),
                    systemLoadScore = await _systemMonitor.GetSystemLoadScoreAsync(),
                    isSystemUnderPressure = await _systemMonitor.IsSystemUnderPressureAsync(),
                    timestamp = DateTime.UtcNow
                };

                return new JsonResult(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metrics");
                return new JsonResult(new { error = "Failed to load metrics" });
            }
        }
    }
}
