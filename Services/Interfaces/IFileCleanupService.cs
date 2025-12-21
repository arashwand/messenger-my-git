using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces
{
    // حذف فایل ها و فولدر های خالی
    public interface IFileCleanupService
    {
        Task CleanupOldFilesAsync(CancellationToken cancellationToken);
    }

}
