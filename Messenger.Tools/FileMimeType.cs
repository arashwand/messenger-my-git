using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Tools
{
    public static class FileMimeType
    {
        /// <summary>
        /// دریافت نوع MIME فایل
        /// </summary>
        /// <param name="filePath">مسیر فایل</param>
        /// <returns>نوع MIME</returns>
        public static string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // لیست برخی از انواع MIME رایج
            var mimeTypes = new Dictionary<string, string>
            {
                { ".txt", "text/plain" },
                { ".pdf", "application/pdf" },
                { ".doc", "application/msword" },
                { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
                { ".xls", "application/vnd.ms-excel" },
                { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                { ".ppt", "application/vnd.ms-powerpoint" },
                { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
                { ".png", "image/png" },
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".gif", "image/gif" },
                { ".bmp", "image/bmp" },
                { ".tiff", "image/tiff" },
                { ".webp", "image/webp" },
                { ".csv", "text/csv" },
                { ".rar", "application/vnd.rar" },
                { ".zip", "application/zip" },
                { ".mp3", "audio/mpeg" },
                { ".wav", "audio/wav" },
                { ".wma", "audio/x-ms-wma" },
                { ".aac", "audio/aac" },
                { ".amr", "audio/amr" },
                { ".ogg", "audio/ogg" },
                { ".m4a", "audio/mp4" },
                { ".3gp", "video/3gpp" },
                { ".mp4", "video/mp4" },
                { ".m4v", "video/x-m4v" },
                { ".mkv", "video/x-matroska" },
                { ".mpeg", "video/mpeg" },
                { ".mpg", "video/mpeg" },
                { ".webm", "video/webm" },
                { ".wmv", "video/x-ms-wmv" }
            };



            return mimeTypes.TryGetValue(extension, out string mimeType) ? mimeType : "application/octet-stream";
        }
    }
}
