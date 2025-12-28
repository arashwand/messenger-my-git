using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services.Classes;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using FluentFTP;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Messenger.Services.Services
{
    public class FileManagementService : IFileManagementService
    {
        private readonly IEMessengerDbContext _context;
        private readonly string _baseUploadPath;
        private readonly ILogger<FileManagementService> _logger;
        private readonly IClassGroupService _classGroupService;
        private readonly FtpUploader _ftpUploader; //سرویس استفاده از FTP
        private readonly IConfiguration _configuration;

        /// <summary>
        /// پسوند تصاویر مجاز
        /// </summary>
        private string[] _allowedImageExtensions;

        /// <summary>
        /// تمام پسوندهایی که مجاز هستند
        /// </summary>
        private string[] _allowedDocExtensions;
        private readonly string _deleteRoot;

        public FileManagementService(IEMessengerDbContext context, ILogger<FileManagementService> logger,
            IClassGroupService classGroupService, IOptions<FileConfigSetting> fileConfigSettings, FtpUploader ftpUploader, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _classGroupService = classGroupService;
            _configuration = configuration;

            _allowedImageExtensions = fileConfigSettings.Value.AllowedImageExtentions;
            _allowedDocExtensions = fileConfigSettings.Value.AllowedExtensions;
            // جهت نگهداری فایلهای اصلی
            _baseUploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            
            _ftpUploader = ftpUploader;


            // جهت نگهداری فایل های کوچک شده مخصوص تصاویر
            //_baseUploadPathThumbnails = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "thumbs");

            _deleteRoot = Path.Combine(_baseUploadPath, "to-delete");

            // اطمینان از وجود دایرکتوری آپلود
            if (!Directory.Exists(_baseUploadPath))
            {
                Directory.CreateDirectory(_baseUploadPath);
            }

            if (!Directory.Exists(_deleteRoot))
            {
                Directory.CreateDirectory(_deleteRoot);
            }


        }


        /// <summary>
        /// دریافت مشخصات فایل براساس مسیر
        /// </summary>
        /// <param name="filePath">مسیر فایل</param>
        /// <returns>اطلاعات فایل</returns>
        public async Task<FileInfoDto> GetFileInfoAsync(string filePath)
        {
            string remotePath = Path.Combine("/uploads/files", filePath).Replace("\\", "/");
            var ftpSettings = _configuration.GetSection("FtpSettings");
            string host = ftpSettings["Host"];
            string username = ftpSettings["Username"];
            string password = ftpSettings["Password"];
            using (var ftp = new AsyncFtpClient(host, username, password))
            {
                await ftp.Connect();
                var size = await ftp.GetFileSize(remotePath);
                var modified = await ftp.GetModifiedTime(remotePath);
                return new FileInfoDto
                {
                    Name = Path.GetFileName(filePath),
                    Length = size,
                    LastWriteTime = modified
                };
            }
        }

        /// <summary>
        /// بررسی وجود فایل
        /// </summary>
        /// <param name="filePath">مسیر فایل</param>
        /// <returns>آیا فایل وجود دارد</returns>
        public async Task<bool> FileExistsAsync(string filePath)
        {
            string remotePath = Path.Combine("/uploads/files", filePath).Replace("\\", "/");
            var ftpSettings = _configuration.GetSection("FtpSettings");
            string host = ftpSettings["Host"];
            string username = ftpSettings["Username"];
            string password = ftpSettings["Password"];
            using (var ftp = new AsyncFtpClient(host, username, password))
            {
                await ftp.Connect();
                return await ftp.FileExists(remotePath);
            }
        }

        public async Task<FileExtensionDto> GetOrCreateFileExtensionAsync(string extension, string contentType)
        {
            try
            {
                Console.WriteLine($"Getting or creating file extension: {extension}");
                extension = extension.ToLower(); // Normalize extension

                // Find existing extension
                var existingExtension = await _context.FileExtensions
                    .FirstOrDefaultAsync(fe => fe.Extension == extension);

                if (existingExtension != null)
                {
                    // Update existing extension if needed
                    //if (existingExtension.Type != contentType)
                    //{
                    //    existingExtension.Type = contentType.Substring(0,49); // 50 کاراکتر اول تایپ رو ذخیره میکنیم
                    //    _context.Update(existingExtension);
                    //    await _context.SaveChangesAsync();
                    //}

                    return new FileExtensionDto
                    {
                        FileExtensionId = existingExtension.FileExtensionId,
                        Extension = existingExtension.Extension,
                        Type = existingExtension.Type
                    };
                }

                // Create new extension if not found

                var fontAwesome = FileIconHelper.GetFontAwesomeIcon(extension); // Helper to determine icon

                var newExtensionEntity = new FileExtension
                {
                    Extension = extension,
                    Type = contentType,// fontAwesome.FileType,
                    FontAwesome = fontAwesome.IconClass,
                    Comment = "Added by system when extention not found."
                };
                _context.FileExtensions.Add(newExtensionEntity);
                await _context.SaveChangesAsync();


                return new FileExtensionDto
                {
                    FileExtensionId = newExtensionEntity.FileExtensionId,
                    Extension = newExtensionEntity.Extension,
                    Type = newExtensionEntity.Type
                };
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        #region tools for check file is image or not

        public bool IsValidImageFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();



            // بررسی پسوند
            if (!_allowedImageExtensions.Contains(extension))
                return false;

            try
            {
                // تلاش برای باز کردن فایل به عنوان تصویر
                using var stream = File.OpenRead(filePath);
                using var image = SixLabors.ImageSharp.Image.Load(stream);
                return true;
            }
            catch
            {
                // اگر باز نشد، تصویر نیست
                return false;
            }
        }

        /// <summary>
        /// ایجاد thumbnail (تصویر کوچک) مربعی شکل
        /// </summary>
        public async Task<bool> CreateThumbnail(string inputPath, string outputPath, int maxHeight = 400, int quality = 60)
        {
            try
            {
                Console.WriteLine($"Thumbnail path =====================: {outputPath}");

                using var image = await SixLabors.ImageSharp.Image.LoadAsync(inputPath);

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

                await image.SaveAsync(outputPath, encoder);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در ایجاد thumbnail: {ex.Message}");
                return false;
            }
        }

        #endregion

        //Todo  تنظیمات بارگذاری روی FTP
       


        /// <summary>
        /// آپلود فایل
        /// </summary>
        /// <param name="file">فایل آپلود شده</param>
        /// <param name="uploaderUserId">نام پوشه با ایدی کاربر ساخته میشه</param>
        /// <returns>مسیر ذخیره شده فایل</returns>
        private async Task<(string newFileName, string filePath, string fileThumbPath)> UploadFileAsync(IFormFile file, string uploaderUserId)
        {
            // استفاده از FTP برای آپلود فایل
            var resultUpload =  await _ftpUploader.UploadFileAsync(file, uploaderUserId);
            return resultUpload;
        }

        /// <summary>
        /// فایل بارگذاری میشود و آیدی آن برگردانده میشود
        /// </summary>
        /// <param name="file"></param>
        /// <param name="uploaderUserId"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<FileIdentifierDto> UploadFileAsync(IFormFile file, long uploaderUserId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty.");

            if (file.Length > 1024 * 1024 * 1024) // 1 GB limit
                throw new ArgumentException("File size exceeds the maximum limit of 1GB.");

            // Your existing file saving logic
            var savedPaths = await UploadFileAsync(file, uploaderUserId.ToString());
            var mimeType = FileMimeType.GetMimeType(savedPaths.filePath);
            string extension = Path.GetExtension(file.FileName);
            var fileExtensionDto = await GetOrCreateFileExtensionAsync(extension, mimeType);

            if (fileExtensionDto == null)
            {
                // Clean up the uploaded file if the type is not supported
                DeleteFile(savedPaths.filePath);
                throw new NotSupportedException($"Unsupported file type: {mimeType}");
            }

            // حالا متد جدید را برای ایجاد رکورد در دیتابیس فراخوانی می‌کنیم
            return await CreateFileRecordAsync(
                uploaderUserId: uploaderUserId,
                newFileName: savedPaths.newFileName,
                filePath: savedPaths.filePath,
                fileThumbPath: savedPaths.fileThumbPath,
                fileSize: file.Length,
                originalFileName: file.FileName,
                mimeType: file.ContentType // یا از GetMimeType(savedPaths.filePath)
            );
        }

        /// <summary>
        /// ذخیره مشخصات فایل بارگذاری شده در دیتابیس
        /// </summary>
        /// <param name="uploaderUserId"></param>
        /// <param name="newFileName"></param>
        /// <param name="filePath"></param>
        /// <param name="fileThumbPath"></param>
        /// <param name="fileSize"></param>
        /// <param name="originalFileName"></param>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<FileIdentifierDto> CreateFileRecordAsync(
            long uploaderUserId,
            string newFileName,
            string filePath,
            string fileThumbPath,
            long fileSize,
            string originalFileName,
            string mimeType)
        {
            string extension = Path.GetExtension(originalFileName); // یا filePath
            var fileExtensionDto = await GetOrCreateFileExtensionAsync(extension, mimeType);

            if (fileExtensionDto == null)
            {
                // فایل فیزیکی را پاک می‌کنیم چون نوع آن پشتیبانی نمی‌شود
                DeleteFile(filePath);
                throw new NotSupportedException($"Unsupported file type: {mimeType}");
            }

            var messageFile = new MessageFile
            {
                MessageId = null,
                UploaderUserId = uploaderUserId,
                FileName = newFileName,
                FilePath = filePath,
                FileThumbPath = fileThumbPath,
                OriginalFileName = originalFileName,
                FileSize = fileSize,
                FileExtensionId = fileExtensionDto.FileExtensionId,
                CreateDate = DateTime.UtcNow,
            };

            _context.MessageFiles.Add(messageFile);
            await _context.SaveChangesAsync();

            return new FileIdentifierDto { FileId = messageFile.MessageFileId };
        }

        /// <summary>
        /// خواندن محتوای فایل جهت نمایش یا دانلود
        /// </summary>
        /// <param name="filePath">مسیر فایل</param>
        /// <returns>محتوای فایل</returns>
        public async Task<byte[]> ReadFileAsync(string filePath)
        {
            // فایل را از FTP دانلود کنیم
            string remotePath = Path.Combine(_baseUploadPath, filePath).Replace("\\", "/");
            return await _ftpUploader.DownloadFileAsync(remotePath);
        }

        /// <summary>
        /// حذف رکورد یک فایل
        /// </summary>
        /// <param name="filePath">مسیر فایل</param>
        /// <returns>نتیجه عملیات</returns>
        public async Task<bool> DeleteMessageFile(long fileId, long uploaderUserId)
        {
            var isOwner = await _context.MessageFiles.FirstOrDefaultAsync(f => f.MessageFileId == fileId
            && f.UploaderUserId == uploaderUserId && f.MessageId == null);
            if (isOwner == null)
                throw new FileNotFoundException($"فایل مورد نظر با آیدی {fileId} یافت نشد.");

            string filePath = $"{uploaderUserId}/{isOwner.FileName}";
            string remotePath = Path.Combine("/msgfiles/uploads/files", filePath).Replace("\\", "/");

            if (!await _ftpUploader.DeleteFileAsync(remotePath))
            {
                return false;
            }

            // حذف thumbnail اگر وجود داشته باشد
            if (!string.IsNullOrEmpty(isOwner.FileThumbPath))
            {
                string thumbRemotePath = Path.Combine("/msgfiles/uploads/thumbs", filePath).Replace("\\", "/");
                await _ftpUploader.DeleteFileAsync(thumbRemotePath);
            }

            _context.Remove(isOwner);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// حذف فایل
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public bool DeleteFile(string filePath)
        {
            string remotePath = Path.Combine("/msgfiles/uploads/files", filePath).Replace("\\", "/");
            return _ftpUploader.DeleteFileAsync(remotePath).Result; // Synchronous for compatibility
        }

        /// <summary>
        /// لیست تمام فایل‌های یک پوشه
        /// </summary>
        /// <param name="subDirectory">زیر پوشه (اختیاری)</param>
        /// <returns>لیست فایل‌ها</returns>
        public async Task<IEnumerable<string>> ListFilesAsync(string subDirectory = null)
        {
            string remoteDirectory = "/uploads/files";
            if (!string.IsNullOrWhiteSpace(subDirectory))
            {
                remoteDirectory = Path.Combine(remoteDirectory, subDirectory).Replace("\\", "/");
            }
            var ftpSettings = _configuration.GetSection("FtpSettings");
            string host = ftpSettings["Host"];
            string username = ftpSettings["Username"];
            string password = ftpSettings["Password"];
            using (var ftp = new AsyncFtpClient(host, username, password))
            {
                await ftp.Connect();
                var files = await ftp.GetListing(remoteDirectory);
                return files.Where(f => f.Type == FtpObjectType.File).Select(f => f.Name);
            }
        }

        /// <summary>
        /// دریافت مسیر کامل فایل
        /// </summary>
        /// <param name="relativePath">مسیر نسبی فایل</param>
        /// <returns>مسیر کامل</returns>
        public string GetFullPath(string relativePath)
        {
            return Path.Combine(_baseUploadPath, relativePath);
        }

        /// <summary>
        /// دریافت نوع MIME فایل
        /// </summary>
        /// <param name="filePath">مسیر فایل</param>
        /// <returns>نوع MIME</returns>
        public string GetMimeType(string filePath)
        {
            return FileMimeType.GetMimeType(filePath);
        }

        /// <summary>
        /// تغییر نام فایل
        /// </summary>
        /// <param name="oldPath">مسیر قدیمی</param>
        /// <param name="newFileName">نام جدید فایل</param>
        /// <returns>مسیر جدید</returns>
        public async Task<string> RenameFileAsync(string oldPath, string newFileName)
        {
            string oldRemote = Path.Combine("/uploads/files", oldPath).Replace("\\", "/");
            string newRemote = Path.Combine("/uploads/files", Path.GetDirectoryName(oldPath), newFileName).Replace("\\", "/");
            var ftpSettings = _configuration.GetSection("FtpSettings");
            string host = ftpSettings["Host"];
            string username = ftpSettings["Username"];
            string password = ftpSettings["Password"];
            using (var ftp = new AsyncFtpClient(host, username, password))
            {
                await ftp.Connect();
                await ftp.MoveFile(oldRemote, newRemote);
            }

            // برگرداندن مسیر نسبی جدید
            string relativePath = Path.Combine(Path.GetDirectoryName(oldPath), newFileName).Replace("\\", "/");
            return relativePath;
        }

        public async Task<FileDownloadData?> GetFileDataAsync1(long messageFileId, long requestorUserId)
        {
            // 1. Find the file and include related data for the permission check.
            var messageFile = await _context.MessageFiles
                .Include(mf => mf.Message)
                .Include(mf => mf.FileExtension) // To get the MIME type
                .FirstOrDefaultAsync(mf => mf.MessageFileId == messageFileId);

            // 2. Basic checks: Does the file exist and is it attached to a message?
            if (messageFile?.Message == null)
            {
                _logger.LogWarning($"File download attempt for non-existent or unattached file ID: {messageFileId}");
                return null; // Not found
            }

            // 3. *** CRUCIAL SECURITY CHECK ***
            //    Verify that the user requesting the file is a member of the class group.
            var classId = messageFile.Message.OwnerId;
            var isMember = await _classGroupService.IsUserMemberOfClassGroupAsync(requestorUserId, classId);

            if (!isMember)
            {
                _logger.LogWarning($"Unauthorized download attempt by User ID: {requestorUserId} for File ID: {messageFileId} in Class ID: {classId}");
                return null; // Access Denied (we return null and the controller will return 404 for security)
            }

            // 4. If permission is granted, read the file from FTP.
            string remotePath = Path.Combine("/msgfiles/uploads/files", messageFile.Message.SenderUserId.ToString(), messageFile.FileName).Replace("\\", "/");

            var fileContent = await _ftpUploader.DownloadFileAsync(remotePath);

            var contentType = messageFile.FileExtension.Type ?? "application/octet-stream";
            var originalFileName = messageFile.OriginalFileName;

            if (messageFile.FileExtension.Extension == ".webm")
            {
                contentType = "video/webm";
            }

            // 5. Return all necessary data to the controller.
            return new FileDownloadData(fileContent, contentType, originalFileName);
        }

        public async Task<FileDownloadStreamDto?> GetFileDataAsync(long messageFileId, long requestorUserId)
        {
            var messageFile = await _context.MessageFiles
                .Include(mf => mf.Message)
                .Include(mf => mf.FileExtension)
                .FirstOrDefaultAsync(mf => mf.MessageFileId == messageFileId);

            if (messageFile?.Message == null)
            {
                _logger.LogWarning($"File not found or unattached: {messageFileId}");
                return null;
            }

            var classId = messageFile.Message.OwnerId;
            var isMember = await _classGroupService.IsUserMemberOfClassGroupAsync(requestorUserId, classId);
            if (!isMember)
            {
                _logger.LogWarning($"Unauthorized access: user {requestorUserId} for file {messageFileId}");
                return null;
            }

            string remotePath = Path.Combine("/msgfiles/uploads/files", messageFile.Message.SenderUserId.ToString(), messageFile.FileName).Replace("\\", "/");

            var contentType = messageFile.FileExtension.Type ?? "application/octet-stream";
            if (messageFile.FileExtension.Extension == ".webm")
                contentType = "video/webm";

            var fileStream = await _ftpUploader.DownloadFileStreamAsync(remotePath);

            return new FileDownloadStreamDto(fileStream, contentType, messageFile.OriginalFileName);
        }

        public async Task<FileDownloadStreamDto?> GetThumbnailDataAsync(long messageFileId, long requestorUserId)
        {
            var messageFile = await _context.MessageFiles
                .Include(mf => mf.Message)
                .Include(mf => mf.FileExtension)
                .FirstOrDefaultAsync(mf => mf.MessageFileId == messageFileId);

            if (messageFile?.Message == null)
            {
                _logger.LogWarning($"Thumbnail download attempt for non-existent or unattached file ID: {messageFileId}");
                return null; // Not found
            }

            // *** CRUCIAL SECURITY CHECK ***
            //    Verify that the user requesting the file is a member of the class group.
            var classId = messageFile.Message.OwnerId;
            var isMember = await _classGroupService.IsUserMemberOfClassGroupAsync(requestorUserId, classId);

            if (!isMember)
            {
                _logger.LogWarning($"Unauthorized thumbnail download attempt by User ID: {requestorUserId} for File ID: {messageFileId} in Class ID: {classId}");
                return null; // Access Denied
            }

            // If permission is granted, read the thumbnail from FTP.
            if (string.IsNullOrEmpty(messageFile.FileThumbPath))
            {
                _logger.LogWarning($"Thumbnail path is empty for file ID: {messageFileId}");
                return null;
            }

            string remotePath = Path.Combine("/msgfiles", messageFile.FileThumbPath).Replace("\\", "/");

            var contentType = messageFile.FileExtension.Type ?? "image/jpeg";
            if (messageFile.FileExtension.Extension == ".webm")
                contentType = "video/webm";

            var fileStream = await _ftpUploader.DownloadFileStreamAsync(remotePath);

            return new FileDownloadStreamDto(fileStream, contentType, messageFile.OriginalFileName);
        }

        public string GetDeletePath(string relativePath)
        {
            return Path.Combine(_deleteRoot, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }

        //استخراج مسیر فایل اصلی از مسیر فایل منتظر حذف
        public string GetOriginalPathFromTemp(string tempPath)
        {
            if (!tempPath.StartsWith(_deleteRoot))
                throw new InvalidOperationException("Path is not inside to-delete folder");

            var relative = tempPath.Substring(_deleteRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            return Path.Combine(_baseUploadPath, relative);
        }


        public string GetDeleteRootPath()
        {
            return _deleteRoot;
        }

        public async Task<bool> MoveFileAsync(string fromPath, string toPath)
        {
            string fromRemote = Path.Combine("/uploads/files", fromPath).Replace("\\", "/");
            string toRemote = Path.Combine("/uploads/files", toPath).Replace("\\", "/");
            var ftpSettings = _configuration.GetSection("FtpSettings");
            string host = ftpSettings["Host"];
            string username = ftpSettings["Username"];
            string password = ftpSettings["Password"];
            using (var ftp = new AsyncFtpClient(host, username, password))
            {
                await ftp.Connect();
                await ftp.MoveFile(fromRemote, toRemote);
                return true;
            }
        }

        public async Task<MessageFileDto?> ProcessAudioChunkAsync(IFormFile file, string recordingId, int chunkIndex, bool isLastChunk, long uploaderUserId)
        {
            // Create a temporary directory for this specific recording session
            var tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads", uploaderUserId.ToString(), "AudioChunks", recordingId);
            Directory.CreateDirectory(tempDirectory);

            // Define the path for the current chunk, padded with zeros for correct ordering
            var chunkFilePath = Path.Combine(tempDirectory, $"{chunkIndex:D5}.tmp");

            // Save the current chunk to the temporary path
            await using (var stream = new FileStream(chunkFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // If this is an intermediate chunk, we're done for now.
            if (!isLastChunk)
            {
                return null;
            }

            // --- Logic for the LAST chunk ---
            _logger.LogInformation($"Processing final chunk for recordingId: {recordingId}");

            // Get all temporary chunk files, order them by name (which is the chunk index)
            var chunkFiles = Directory.GetFiles(tempDirectory, "*.tmp").OrderBy(f => f).ToList();

            // Create a MemoryStream to hold the final, combined file
            await using var finalFileStream = new MemoryStream();

            // Combine all chunks into the final stream
            foreach (var chunkFile in chunkFiles)
            {
                await using (var chunkStream = new System.IO.FileStream(chunkFile, FileMode.Open, FileAccess.Read))
                {
                    await chunkStream.CopyToAsync(finalFileStream);
                }
            }
            finalFileStream.Position = 0; // Reset the stream position to the beginning

            // To use your existing service logic, create an IFormFile from the final MemoryStream
            var finalFormFile = new FormFile(finalFileStream, 0, finalFileStream.Length, "voice.webm", $"{recordingId}.webm")
            {
                Headers = new HeaderDictionary(),
                ContentType = "audio/webm" // You can also pass this from the client if needed
            };

            // Use your existing 'UploadFileAsync' method to save the final file and create a DB record
            var fileIdentifier = await UploadFileAsync(finalFormFile, uploaderUserId);

            if (fileIdentifier == null || fileIdentifier.FileId <= 0)
            {
                Directory.Delete(tempDirectory, true); // Clean up temp files on failure
                _logger.LogError($"Failed to save the final assembled audio file for recordingId: {recordingId}");
                return null;
            }

            // --- Calculate Audio Duration using NAudio ---
            finalFileStream.Position = 0; // Reset stream again for reading
            double durationInSeconds = 0;
            string durationFormatted = "0:00";
            try
            {
                using (var waveFileReader = new WaveFileReader(finalFileStream))
                {
                    durationInSeconds = waveFileReader.TotalTime.TotalSeconds;
                    durationFormatted = $"{(int)waveFileReader.TotalTime.TotalMinutes}:{waveFileReader.TotalTime.Seconds:D2}";
                }
            }
            catch (Exception waveEx)
            {
                _logger.LogWarning(waveEx, $"NAudio could not read the final audio stream for {recordingId}. Defaulting duration.");
            }

            // Clean up the temporary directory
            Directory.Delete(tempDirectory, true);

            // Return a DTO with the necessary information for the SignalR message
            return new MessageFileDto
            {
                MessageFileId = fileIdentifier.FileId,
                Duration = durationInSeconds,
                DurationFormatted = durationFormatted
            };
        }


        public async Task<FileIdentifierDto?> ProcessFileChunkAsync(IFormFile file, string uploadId, int chunkIndex, int totalChunks, string originalFileName, long uploaderUserId)
        {
            // Define a temporary directory for this specific upload
            var tempDirectory = Path.Combine(Path.GetTempPath(), "FileChunks", uploadId);
            Directory.CreateDirectory(tempDirectory);

            // Define the path for the current chunk, padded with zeros for correct ordering
            var chunkFilePath = Path.Combine(tempDirectory, $"{chunkIndex:D5}.tmp");

            // Save the current chunk to the temporary path
            await using (var stream = new FileStream(chunkFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // If this is an intermediate chunk, we're done for now. Return null.
            // The controller will return Ok() to the client.
            bool isLastChunk = (chunkIndex == totalChunks - 1);
            if (!isLastChunk)
            {
                return null;
            }

            // --- Logic for the LAST chunk ---
            _logger.LogInformation($"Processing final chunk for uploadId: {uploadId}");

            // Get all temporary chunk files, order them by name (which is the chunk index)
            var chunkFiles = Directory.GetFiles(tempDirectory, "*.tmp").OrderBy(f => f).ToList();

            // Sanity check: ensure we have all the chunks
            if (chunkFiles.Count != totalChunks)
            {
                _logger.LogError($"Mismatch in chunk count for uploadId {uploadId}. Expected {totalChunks}, got {chunkFiles.Count}.");
                Directory.Delete(tempDirectory, true); // Clean up
                throw new InvalidOperationException("Chunk count mismatch. Upload failed.");
            }

            // Create a MemoryStream to hold the final, combined file
            await using var finalFileStream = new MemoryStream();

            // Combine all chunks into the final stream
            foreach (var chunkFile in chunkFiles)
            {
                await using (var chunkStream = new System.IO.FileStream(chunkFile, FileMode.Open, FileAccess.Read))
                {
                    await chunkStream.CopyToAsync(finalFileStream);
                }
            }
            finalFileStream.Position = 0; // Reset the stream position to the beginning

            // To use your existing service logic, create an IFormFile from the final MemoryStream
            var finalFormFile = new FormFile(finalFileStream, 0, finalFileStream.Length, "file", originalFileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = file.ContentType // Use the content type of the last chunk
            };

            // *** IMPORTANT: Use your existing 'UploadFileAsync' method to save the final file and create a DB record ***
            // This assumes you have a method like this that takes an IFormFile and a user ID.
            var messageFile = await UploadFileAsync(finalFormFile, uploaderUserId);

            // Clean up the temporary directory regardless of success or failure
            try
            {
                Directory.Delete(tempDirectory, true);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, $"Failed to clean up temporary directory {tempDirectory} for uploadId {uploadId}.");
            }

            if (messageFile == null || messageFile.FileId <= 0)
            {
                _logger.LogError($"Failed to save the final assembled file for uploadId: {uploadId}");
                return null;
            }

            // Return the MessageFile entity (or a DTO) with the necessary information
            return messageFile;
        }


        public async Task<SharedContentDto> GetSharedContentForChatAsync(long chatId, string groupType)
        {
            var sharedContent = new SharedContentDto();

            // Determine the message query based on group type
            IQueryable<Message> messagesQuery;
            if (groupType.Equals(ConstChat.ClassGroupType, StringComparison.OrdinalIgnoreCase))
            {
                messagesQuery = _context.Messages.Where(m => m.MessageType == 0 && !m.IsHidden &&
                m.OwnerId == chatId).Include(mf => mf.MessageFiles);
            }
            else if (groupType.Equals(ConstChat.ClassGroupType, StringComparison.OrdinalIgnoreCase))
            {
                messagesQuery = _context.Messages.Where(m => m.MessageType == 1 && !m.IsHidden &&
                    m.OwnerId == chatId).Include(mf => mf.MessageFiles);
            }
            else
            {
                // MessageType == 2
                //TODO برای چت دونفره هم ایجاد شود
                return sharedContent; // Or throw an exception for invalid groupType
            }

            // Fetch files
            var files = await messagesQuery
                .SelectMany(m => m.MessageFiles)
                //.Where(f => f.Message.IsHidden == false)
                .OrderByDescending(f => f.Message.MessageDateTime)
                .Select(f => new SharedFileDto
                {
                    MessageFileId = f.MessageFileId,
                    FileName = f.FileName,
                    OriginalFileName = f.OriginalFileName,
                    //FilePath = f.FilePath,
                    FileThumbPath = f.FileThumbPath,
                    FileSize = f.FileSize,
                    SentAt = f.Message.MessageDateTime
                })
                .ToListAsync();

            // Categorize files
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (_allowedImageExtensions.Contains(extension))
                {
                    file.FileType = "media";
                    sharedContent.MediaFiles.Add(file);
                }
                else if (_allowedDocExtensions.Contains(extension))
                {
                    file.FileType = "document";
                    sharedContent.DocumentFiles.Add(file);
                }
            }

            // Fetch links
            var links = await messagesQuery
                .Where(m => m.MessageTexts.FirstOrDefault().MessageTxt.Contains("http://") || m.MessageTexts.FirstOrDefault().MessageTxt.Contains("https://"))
                .OrderByDescending(m => m.MessageDateTime)
                .Select(m => new
                {
                    m.MessageId,
                    m.MessageTexts.FirstOrDefault().MessageTxt,
                    m.MessageDateTime
                })
                .ToListAsync();

            // Extract links from message text (a simple regex can do this)
            var urlRegex = new Regex(@"(https?:\/\/[^\s]+)");
            foreach (var message in links)
            {
                var matches = urlRegex.Matches(message.MessageTxt);
                foreach (Match match in matches)
                {
                    sharedContent.Links.Add(new SharedLinkDto
                    {
                        MessageId = message.MessageId,
                        LinkUrl = match.Value,
                        SentAt = message.MessageDateTime
                    });
                }
            }

            return sharedContent;
        }

        /// <summary>
        /// محاسبه تعداد فایلهای یک چت
        /// </summary>
        /// <param name="chatId"></param>
        /// <param name="groupType"></param>
        /// <returns></returns>
        public async Task<CountSharedContentDto> GetCountSharedContentForChatAsync(long chatId, string groupType)
        {
            var countSharedContent = new CountSharedContentDto();

            // Determine the message query based on group type
            IQueryable<Message> messagesQuery;
            if (groupType.Equals(ConstChat.ClassGroupType, StringComparison.OrdinalIgnoreCase))
            {
                messagesQuery = _context.Messages.Where(m => m.MessageType == 0 && !m.IsHidden &&
                m.OwnerId == chatId).Include(mf => mf.MessageFiles);
            }
            else if (groupType.Equals(ConstChat.ClassGroupType, StringComparison.OrdinalIgnoreCase))
            {
                messagesQuery = _context.Messages.Where(m => m.MessageType == 1 && !m.IsHidden &&
                    m.OwnerId == chatId).Include(mf => mf.MessageFiles);
            }
            else
            {
                // MessageType == 2
                //TODO برای چت دونفره هم ایجاد شود
                return countSharedContent; // Or throw an exception for invalid groupType
            }

            // Fetch files
            var files = await messagesQuery
                .SelectMany(m => m.MessageFiles)
                .ToListAsync();

            countSharedContent.MediaFilesCount = files.Count(f => _allowedImageExtensions.Contains(Path.GetExtension(f.FileName).ToLower()));
            countSharedContent.DocumentFilesCount = files.Count(f => _allowedDocExtensions.Contains(Path.GetExtension(f.FileName).ToLower()));

            // Fetch links
            countSharedContent.LinkFilesCount = await messagesQuery
                .SelectMany(m => m.MessageTexts)
                .CountAsync(mt => mt.MessageTxt.Contains("http://") || mt.MessageTxt.Contains("https://"));



            return countSharedContent;
        }
    }

}
