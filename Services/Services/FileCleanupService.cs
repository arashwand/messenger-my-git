using Messenger.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Services
{
    public class FileCleanupService : IFileCleanupService
    {
        private readonly ILogger<FileCleanupService> _logger;
        private readonly IFileManagementService _fileService;

        public FileCleanupService(
            ILogger<FileCleanupService> logger,
            IFileManagementService fileService)
        {
            _logger = logger;
            _fileService = fileService;
        }

        public Task CleanupOldFilesAsync(CancellationToken cancellationToken)
        {
            var deleteRoot = _fileService.GetDeleteRootPath();

            if (!Directory.Exists(deleteRoot))
                return Task.CompletedTask;

            var files = Directory.GetFiles(deleteRoot, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < DateTime.Now.AddMinutes(-30))
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogInformation($"Deleted file: {file}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error deleting file: {file}");
                    }
                }
            }

            // حذف فولدرهای خالی پس از حذف فایل‌ها
            DeleteEmptyDirectories(deleteRoot);

            return Task.CompletedTask;
        }

        private void DeleteEmptyDirectories(string rootPath)
        {
            foreach (var dir in Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories))
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    try
                    {
                        Directory.Delete(dir);
                        _logger.LogInformation($"Deleted empty directory: {dir}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to delete empty directory: {dir}");
                    }
                }
            }
        }
    }

}
