using Azure.Core;
using Messenger.API.Hubs;
using Messenger.API.InnerDto;
using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Messenger.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Messenger.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FileManagementController : ControllerBase
    {
        private readonly IFileManagementService _fileService;
        private readonly ILogger<FileManagementController> _logger;


        public FileManagementController(IFileManagementService fileService,
            ILogger<FileManagementController> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        /// <summary>
        /// آپلود فایل
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(1024 * 1024 * 1024)] // 1GB limit
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId <= 0) return Unauthorized();

                if (file == null || file.Length == 0)
                {
                    return BadRequest("فایلی ارسال نشده است");
                }


                //---آیدی فایل بعد از بارگذاری موفق برگردانده میشود 
                var resultUpload = await _fileService.UploadFileAsync(file, userId);

                // var fileInfo = _fileService.GetFileInfo(filePath);

                return Ok(

                    resultUpload
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطا در آپلود فایل: {ex.Message}");
            }
        }

      
        [HttpGet("GetSharedFiles")]
        public async Task<IActionResult> GetSharedFiles([FromQuery] long chatId, [FromQuery] string groupType)
        {
            if (chatId <= 0 || string.IsNullOrEmpty(groupType))
                return BadRequest("Valid Chat ID and Group Type are required.");

            try
            {
                var sharedContent = await _fileService.GetSharedContentForChatAsync(chatId, groupType);
                return Ok(sharedContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching shared content for chat {ChatId} of type {GroupType}", chatId, groupType);
                return StatusCode(500, "An internal error occurred while fetching shared content.");
            }
        }

        /// <summary>
        /// nvdhtj junhn thdgihd d; ]j
        /// </summary>
        /// <param name="chatId"></param>
        /// <param name="groupType"></param>
        /// <returns></returns>
        [HttpGet("GetCountSharedFiles")]
        public async Task<IActionResult> GetCountSharedFiles([FromQuery] long chatId, [FromQuery] string groupType)
        {
            if (chatId <= 0 || string.IsNullOrEmpty(groupType))
                return BadRequest("Valid Chat ID and Group Type are required.");

            try
            {
                var sharedContent = await _fileService.GetCountSharedContentForChatAsync(chatId, groupType);
                return Ok(sharedContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching shared content for chat {ChatId} of type {GroupType}", chatId, groupType);
                return StatusCode(500, "An internal error occurred while fetching shared content.");
            }
        }


        /// <summary>
        /// دریافت اطلاعات فایل
        /// </summary>
        [HttpGet("info")]
        public async Task<IActionResult> GetFileInfo([FromQuery] string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return BadRequest("مسیر فایل مشخص نشده است");
                }

                if (!await _fileService.FileExistsAsync(filePath))
                {
                    return NotFound($"فایل در مسیر {filePath} یافت نشد");
                }

                var fileInfo = await _fileService.GetFileInfoAsync(filePath);

                return Ok(new
                {
                    FilePath = filePath,
                    FullPath = "", // No full path for FTP
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    CreatedDate = DateTime.Now, // FTP may not have creation time
                    ModifiedDate = fileInfo.LastWriteTime,
                    MimeType = _fileService.GetMimeType(filePath)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطا در دریافت اطلاعات فایل: {ex.Message}");
            }
        }

        /// <summary>
        /// دانلود فایل
        /// </summary>
        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile([FromQuery] int messageFileId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId <= 0) return Unauthorized();

                if (messageFileId <= 0)
                {
                    return BadRequest("مسیر فایل مشخص نشده است");
                }

                var downloadData = await _fileService.GetFileDataAsync(messageFileId, userId);
                if (downloadData == null)
                    return NotFound("File not found or access denied.");

                return File(
                    downloadData.ContentStream,
                    downloadData.ContentType,
                    downloadData.FileName,
                    enableRangeProcessing: true
                );

            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطا در دانلود فایل: {ex.Message}");
            }
        }

        /// <summary>
        /// دانلود thumbnail فایل
        /// </summary>
        [HttpGet("download-thumbnail")]
        public async Task<IActionResult> DownloadThumbnail([FromQuery] long messageFileId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId <= 0) return Unauthorized();

                if (messageFileId <= 0)
                {
                    return BadRequest("ایدی فایل ارسال نشده است");
                }

                var downloadData = await _fileService.GetThumbnailDataAsync(messageFileId, userId);
                if (downloadData == null)
                    return NotFound("Thumbnail not found or access denied.");

                return File(downloadData.ContentStream, downloadData.ContentType, downloadData.FileName, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطا در دانلود thumbnail: {ex.Message}");
            }
        }

        /// <summary>
        /// حذف فایل
        /// </summary>
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteFile([FromQuery] long fileId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId <= 0) return Unauthorized();

                if (fileId <= 0)
                {
                    return BadRequest("ایدی فایل ارسال نشده است");
                }

                bool result = await _fileService.DeleteMessageFile(fileId, userId);

                if (result)
                {
                    return Ok(new { Message = $"فایل {fileId} با موفقیت حذف شد" });
                }
                else
                {
                    return StatusCode(500, "خطا در حذف فایل");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطا در حذف فایل: {ex.Message}");
            }
        }

        /// <summary>
        /// لیست فایل‌های موجود
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> ListFiles([FromQuery] string subDirectory = null)
        {
            try
            {
                var files = await _fileService.ListFilesAsync(subDirectory);

                var fileList = files.Select(f => new
                {
                    FileName = f,
                    FileSize = 0, // FTP doesn't give size in list
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    MimeType = _fileService.GetMimeType(f)
                });

                return Ok(fileList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطا در دریافت لیست فایل‌ها: {ex.Message}");
            }
        }

        /// <summary>
        /// تغییر نام فایل
        /// </summary>
        [HttpPut("rename")]
        public async Task<IActionResult> RenameFile([FromQuery] string filePath, [FromQuery] string newFileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(newFileName))
                {
                    return BadRequest("مسیر فایل یا نام جدید مشخص نشده است");
                }

                if (!await _fileService.FileExistsAsync(filePath))
                {
                    return NotFound($"فایل در مسیر {filePath} یافت نشد");
                }

                string newPath = await _fileService.RenameFileAsync(filePath, newFileName);

                return Ok(new
                {
                    OldPath = filePath,
                    NewPath = newPath,
                    Message = $"فایل با موفقیت به {newFileName} تغییر نام داده شد"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطا در تغییر نام فایل: {ex.Message}");
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return 0;
        }

        
        [HttpPost("UploadAudioChunk")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10 * 1024 * 1024)] // Set a reasonable size limit for each chunk
        public async Task<IActionResult> UploadAudioChunk([FromForm] UploadAudioFileChunkViewModel request)
        {
            

            if (request.file == null || string.IsNullOrEmpty(request.recordingId))
                return BadRequest("File, recordingId, and chunkIndex are required.");

            try
            {
                var file = request.file;
                var recordingId = request.recordingId;
                var chunkIndex = request.chunkIndex;
                var isLastChunk = request.isLastChunk;

                // Get the authenticated user's ID from the JWT claims
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!long.TryParse(userIdString, out var userId))
                {
                    return Unauthorized("Invalid user identifier in token.");
                }

                // Call the service method to process the chunk
                var resultDto = await _fileService.ProcessAudioChunkAsync(file, recordingId, chunkIndex, isLastChunk, userId);

                // If it was the last chunk, the service method will return a DTO.
                if (isLastChunk)
                {
                    if (resultDto == null)
                    {
                        // If processing the final chunk failed in the service
                        return StatusCode(500, new { success = false, message = "Failed to process the final audio file." });
                    }

                    return Ok(new
                    {
                        userId = userId.ToString(),
                        success = true,
                        fileId = resultDto.MessageFileId,
                        duration = resultDto.Duration,
                        durationFormatted = resultDto.DurationFormatted,
                        recordingId = recordingId
                    });

                }
                else
                {
                    // For intermediate chunks, just acknowledge receipt.
                    return Ok(new { success = true, message = "Chunk received." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audio chunk for recordingId {RecordingId}", request.recordingId);
                return StatusCode(500, new { success = false, message = "An internal server error occurred while processing the audio chunk." });
            }
        }


        // [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("UploadFileChunk")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(1024 * 1024 * 1024)] // Same limit as the proxy
        public async Task<IActionResult> UploadFileChunk(
            [FromForm] UploadFileChunkViewModel request)
        {
            if (request.file == null || string.IsNullOrEmpty(request.uploadId) || string.IsNullOrEmpty(request.originalFileName))
                return BadRequest("File chunk, uploadId, and originalFileName are required.");

            try
            {

                var file = request.file;
                var uploadId = request.uploadId;
                var chunkIndex = request.chunkIndex;
                var totalChunks = request.totalChunks;
                var originalFileName = request.originalFileName;


                // Get the authenticated user's ID from the JWT claims
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!long.TryParse(userIdString, out var userId))
                {
                    return Unauthorized("Invalid user identifier in token.");
                }

                // Call the service method to process the chunk
                var finalFileResult = await _fileService.ProcessFileChunkAsync(file, uploadId, chunkIndex, totalChunks, originalFileName, userId);

                bool isLastChunk = (chunkIndex == totalChunks - 1);

                if (isLastChunk)
                {
                    if (finalFileResult == null)
                    {
                        return StatusCode(500, new { success = false, message = "Failed to process the final file." });
                    }

                    // For the last chunk, return the file details (or a DTO) to the client.
                    // The client needs the 'fileId' to attach to the message.
                    return Ok(new
                    {
                        success = true,
                        message = "File uploaded successfully.",
                        fileId = finalFileResult.FileId // Assuming the entity has an 'Id' property
                    });
                }
                else
                {
                    // For intermediate chunks, just acknowledge receipt.
                    return Ok(new { success = true, message = "Chunk received." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file chunk for uploadId {UploadId}", request.uploadId);
                return StatusCode(500, new { success = false, message = "An internal server error occurred while processing the file chunk." });
            }
        }


    }
}
