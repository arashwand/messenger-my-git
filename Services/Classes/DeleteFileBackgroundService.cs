using Messenger.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Classes
{
    /// <summary>
    /// تسک حذف همه فایل های درون پوشه حذف بعد از گذشت 30 دقیقه
    /// directory name : uploads/to-delete
    /// </summary>
    public class DeleteFileBackgroundService : BackgroundService
    {
        private readonly ILogger<DeleteFileBackgroundService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public DeleteFileBackgroundService(
            ILogger<DeleteFileBackgroundService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();

                try
                {
                    var cleanupService = scope.ServiceProvider.GetRequiredService<IFileCleanupService>();
                    await cleanupService.CleanupOldFilesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during file cleanup");
                }

                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }
    }
}
