using Messenger.DTOs;
using Messenger.Models.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces
{
    public interface IFileManagementService
    {
        Task<FileInfoDto> GetFileInfoAsync(string filePath);
        Task<bool> FileExistsAsync(string filePath);

        Task<MessageFileDto?> ProcessAudioChunkAsync(IFormFile file, string recordingId, int chunkIndex, bool isLastChunk, long uploaderUserId);
        Task<FileIdentifierDto> UploadFileAsync(IFormFile file, long uploaderUserId);
        Task<FileIdentifierDto> CreateFileRecordAsync(
            long uploaderUserId,
            string newFileName,
            string filePath,
            string fileThumbPath,
            long fileSize,
            string originalFileName,
            string mimeType);

        Task<FileIdentifierDto?> ProcessFileChunkAsync(IFormFile file, string uploadId, int chunkIndex, int totalChunks, string originalFileName, long uploaderUserId);

        Task<SharedContentDto> GetSharedContentForChatAsync(int chatId, string groupType);
        Task<CountSharedContentDto> GetCountSharedContentForChatAsync(int chatId, string groupType);


        Task<byte[]> ReadFileAsync(string filePath);
        Task<bool> DeleteMessageFile(long fileId, long uploaderUserId);
        bool DeleteFile(string filePath);
        Task<IEnumerable<string>> ListFilesAsync(string subDirectory = null);
        string GetFullPath(string relativePath);
        string GetMimeType(string filePath);
        Task<string> RenameFileAsync(string oldPath, string newFileName);
        //Task<FileDownloadData?> GetFileDataAsync(long messageFileId, long requestorUserId);
        Task<FileDownloadStreamDto?> GetFileDataAsync(long messageFileId, long requestorUserId);

        // برای حذف مطمئن فایلها در زمان ویرایش پیام- ابتدا فایل ها را به پوشه بک اپ منتقل میکنیم در نهایت حذف کامل انجام میدهیم
        string GetDeletePath(string relativePath);
        string GetOriginalPathFromTemp(string tempPath);
        string GetDeleteRootPath();
        Task<bool> MoveFileAsync(string fromPath, string toPath);
        Task<FileDownloadStreamDto?> GetThumbnailDataAsync(long messageFileId, long requestorUserId);
    }
}
