using FluentFTP;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Messenger.Tools;

namespace Messenger.Services.Classes
{
    public class FtpUploader
    {
        private readonly string _host;
        private readonly string _username;
        private readonly string _password;
        private readonly string _baseUploadPath;
        private readonly string _baseUploadPathThumbnails;
        private readonly string[] _allowedDocExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rar",".zip", ".webm", ".ogg", ".mp3", ".wav" };

        public FtpUploader(string host, string username, string password, string baseUploadPath, string baseUploadPathThumbnails, string[] allowedDocExtensions)
        {
            _host = host;
            _username = username;
            _password = password;
            _baseUploadPath = baseUploadPath;
            _baseUploadPathThumbnails = baseUploadPathThumbnails;
            if (allowedDocExtensions.Length > 0)
            {
                _allowedDocExtensions = allowedDocExtensions;
            }
        }

        public async Task<(string newFileName, string filePath, string fileThumbPath)> UploadFileAsync(IFormFile file, string uploaderUserId)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("فایل معتبر نیست");
                }

                if (string.IsNullOrWhiteSpace(uploaderUserId))
                {
                    throw new ArgumentException("آیدی کاربر معتبر نیست");
                }

                string extension = Path.GetExtension(file.FileName).ToLower();
                if (!_allowedDocExtensions.Contains(extension))
                {
                    throw new ArgumentException("پسوند فایل مجاز نیست");
                }

                var fileName = $"{Guid.NewGuid()}{extension}";
                var userDirectory = Path.Combine(_baseUploadPath, uploaderUserId).Replace("\\", "/");
                var remoteFilePath = $"{userDirectory}/{fileName}";

                using (var ftp = new AsyncFtpClient(_host, _username, _password))
                {
                    await ftp.Connect();

                    if (!await ftp.DirectoryExists(userDirectory))
                    {
                        await ftp.CreateDirectory(userDirectory);
                    }

                    using (var stream = file.OpenReadStream())
                    {
                        await ftp.UploadStream(stream, remoteFilePath, FtpRemoteExists.Overwrite, true);
                    }
                }

                string relativePath = Path.Combine(uploaderUserId, fileName).Replace("\\", "/");
                string relativeThumbPath = "";
                var fileType = FileMimeType.GetMimeType(file.FileName);
                if (IsValidImageFile(fileType))
                {
                    var thumbnailFolder = Path.Combine(_baseUploadPathThumbnails, uploaderUserId).Replace("\\", "/");
                    var thumbnailRemotePath = $"{thumbnailFolder}/{fileName}";

                    using (var ftp = new AsyncFtpClient(_host, _username, _password))
                    {
                        await ftp.Connect();

                        if (!await ftp.DirectoryExists(thumbnailFolder))
                        {
                            await ftp.CreateDirectory(thumbnailFolder);
                        }

                        // Create thumbnail stream
                        using (var thumbnailStream = await CreateThumbnailStream(file.OpenReadStream()))
                        {
                            await ftp.UploadStream(thumbnailStream, thumbnailRemotePath, FtpRemoteExists.Overwrite, true);
                        }
                    }

                    relativeThumbPath = Path.Combine("uploads", "thumbs", uploaderUserId, fileName).Replace("\\", "/");
                }

                return (fileName, relativePath, relativeThumbPath);
            }
            catch (Exception ex)
            {

                throw new Exception(ex.Message);
            }
        }

        private bool IsValidImageFile(string contentType)
        {
            return contentType.StartsWith("image/");
        }

        private async Task<Stream> CreateThumbnailStream(Stream inputStream, int maxHeight = 400, int quality = 60)
        {
            var outputStream = new MemoryStream();

            using var image = await SixLabors.ImageSharp.Image.LoadAsync(inputStream);

            // محاسبه عرض جدید با حفظ نسبت ابعاد بر اساس حداکثر ارتفاع
            int newWidth = (int)(image.Width * ((float)maxHeight / image.Height));

            // تغییر اندازه تصویر با حفظ نسبت ابعاد
            image.Mutate(x => x
                .Resize(new ResizeOptions
                {
                    Size = new Size(newWidth, maxHeight),
                    Mode = ResizeMode.Max // حفظ نسبت ابعاد و محدود کردن به حداکثر اندازه
                }));

            var encoder = new JpegEncoder
            {
                Quality = quality
            };

            await image.SaveAsync(outputStream, encoder);
            outputStream.Position = 0; // Reset position for reading
            return outputStream;
        }

        public async Task<byte[]> DownloadFileAsync(string remotePath)
        {
            using (var ftp = new AsyncFtpClient(_host, _username, _password))
            {
                await ftp.Connect();

                using (var stream = new MemoryStream())
                {
                    await ftp.DownloadStream(stream, remotePath);
                    return stream.ToArray();
                }
            }
        }

        public async Task<Stream> DownloadFileStreamAsync(string remotePath)
        {
            var ftp = new AsyncFtpClient(_host, _username, _password);
            await ftp.Connect();

            var stream = new MemoryStream();
            await ftp.DownloadStream(stream, remotePath);
            stream.Position = 0;
            // Note: ftp client should be disposed properly, but for simplicity, assuming the stream is used immediately.
            return stream;
        }

        public async Task<bool> DeleteFileAsync(string remotePath)
        {
            using (var ftp = new AsyncFtpClient(_host, _username, _password))
            {
                await ftp.Connect();

                if (await ftp.FileExists(remotePath))
                {
                    await ftp.DeleteFile(remotePath);
                    return true;
                }
                return false;
            }
        }

        public async Task<bool> FileExistsAsync(string remotePath)
        {
            using (var ftp = new AsyncFtpClient(_host, _username, _password))
            {
                await ftp.Connect();
                return await ftp.FileExists(remotePath);
            }
        }

        public async Task<IEnumerable<string>> ListFilesAsync(string remoteDirectory)
        {
            using (var ftp = new AsyncFtpClient(_host, _username, _password))
            {
                await ftp.Connect();
                var files = await ftp.GetListing(remoteDirectory);
                return files.Where(f => f.Type == FtpObjectType.File).Select(f => f.Name);
            }
        }

        public async Task<bool> MoveFileAsync(string fromPath, string toPath)
        {
            using (var ftp = new AsyncFtpClient(_host, _username, _password))
            {
                await ftp.Connect();
                await ftp.MoveFile(fromPath, toPath);
                return true;
            }
        }
    }
}
