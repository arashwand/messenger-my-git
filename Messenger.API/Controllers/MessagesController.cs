using Messenger.API.Hubs;
using Messenger.API.RequestDTOs;
using Messenger.API.ServiceHelper;
using Messenger.API.ServiceHelper.Interfaces;
using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services;
using Messenger.Services.Classes;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Messenger.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageService _messageService;
        private readonly IBroadcastService _broadcastService;
        private readonly IFileManagementService _fileService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly RedisLastMessageService _redisLastMessage;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(
            IMessageService messageService,
            IFileManagementService fileService,
            IHubContext<ChatHub> hubContext,
            RedisLastMessageService redisLastMessage,
            ILogger<MessagesController> logger,
            IBroadcastService broadcastService)
        {
            _messageService = messageService;
            _fileService = fileService;
            _hubContext = hubContext;
            _redisLastMessage = redisLastMessage;
            _logger = logger;
            _broadcastService = broadcastService;
        }

        private long GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && long.TryParse(userIdClaim.Value, out long userId))
            {
                return userId;
            }
            return 0;
        }

        /// <summary>
        /// بررسی میکنه درخواست کننده اکانت پرتال هست یا خیر
        /// </summary>
        private bool IsPortalAcc()
        {
            var user = HttpContext.User;
            bool isPortalAccount = user.HasClaim("scope", "system_bot");
            return isPortalAccount;
        }

        /// <summary>
        /// ارسال پیام به گروه یا کانال مشخص
        /// ClassGroup - ChannelGroup
        /// </summary>
        [HttpPost("send-message-portal")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<IActionResult> SendMessageFromPortal([FromBody] SendMessageFromPortalDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid request data.");

            if (request.TargetId <= 0)
                return BadRequest("GroupId is required.");

            if (string.IsNullOrWhiteSpace(request.MessageText))
                return BadRequest("Message text cannot be empty.");

            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            try
            {
                var requestmodel = new SendMessageToTargetDto
                {
                    TargetId = request.TargetId,
                    TargetType = request.GroupType,
                    MessageText = request.MessageText,
                    IsPin = request.IsPin,
                    IsPortalMessage = true
                };
                var savedMessageDto = await _broadcastService.SendMessageAsync(
                    senderUserId: userId, requestmodel
                );

                if (savedMessageDto == null)
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to save message.");

                string groupKey = GenerateSignalRGroupKey.GenerateKey(request.TargetId, request.GroupType);
                await _hubContext.Clients.Group(groupKey).SendAsync("ReceiveMessage", savedMessageDto);

                return Ok(new
                {
                    Message = "Message sent successfully.",
                    Data = savedMessageDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending message to group {GroupId}", request.TargetId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        /// <summary>
        /// ارسال پیام به همه گروه‌ها، کانال‌ها یا کاربران بر اساس نقش
        /// </summary>
        [HttpPost("send-message-all-portal")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<IActionResult> SendMessageToAllFromPortal([FromBody] SendMessageToAllFromPortalDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid request data.");

            if (string.IsNullOrWhiteSpace(request.MessageText))
                return BadRequest("Message text cannot be empty.");

            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            try
            {
                var savedMessageDto = await _broadcastService.BroadcastAsync(userId, request);

                if (savedMessageDto == null)
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to save message.");

                return Ok(new
                {
                    Message = savedMessageDto.MessageText,
                    Data = savedMessageDto.TargetIdsCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending message to MessageType {MessageType}", request.MessageType);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }


        /// <summary>
        /// ارسال پیام به لیستی از ایدی کاربران
        /// </summary>
        [HttpPost("send-private-message-all-portal")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<IActionResult> SendMessageToAllFromPortal([FromBody] SendPrivateMessageToAllFromPortalDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid request data.");

            if (string.IsNullOrWhiteSpace(request.MessageText))
                return BadRequest("Message text cannot be empty.");

            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            try
            {
                var savedMessageDto = await _broadcastService.BroadcasPrivateMessagetAsync(userId, request);

                if (savedMessageDto == null)
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to save message.");

                return Ok(new
                {
                    Message = savedMessageDto.MessageText,
                    Data = savedMessageDto.TargetIdsCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending message to MessageType {MessageType}", request.MessageType);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }


        [HttpGet("{messageId}")]
        public async Task<ActionResult<MessageDto>> GetMessageById(long messageId)
        {
            var userId = GetCurrentUserId();
            var message = await _messageService.GetMessageByIdAsync(userId, messageId);
            if (message == null) return NotFound();
            return Ok(message);
        }

        [HttpGet("private/{otherUserId}")]
        public async Task<ActionResult<PrivateChatDto>> GetPrivateMessages(long otherUserId, [FromQuery] int pageSize = 50, [FromQuery] long messageId = 0, [FromQuery] bool loadOlder = false, [FromQuery] bool loadBothDirections = false)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            var privateChatDto = await _messageService.GetPrivateMessagesAsync(userId, otherUserId, pageSize, messageId, loadOlder, loadBothDirections);
            return Ok(privateChatDto);
        }

        [HttpGet("private/conversation/{conversationId}")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetPrivateMessagesByConversationId(long conversationId, [FromQuery] int pageSize = 50, [FromQuery] long messageId = 0, [FromQuery] bool loadOlder = false, [FromQuery] bool loadBothDirections = false)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            var messages = await _messageService.GetPrivateChatMessagesAsync(conversationId, userId, pageSize, messageId, loadOlder, loadBothDirections);
            return Ok(messages);
        }

        


        /// <summary>
        /// دریافت پیامهای گروه یا کانال مشخص
        /// </summary>
        /// <param name="chatId"></param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <param name="messageId"></param>
        /// <param name="loadOlder"></param>
        /// <returns></returns>
        [HttpGet("chatMessages/{chatId}")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetClassGroupMessages(int chatId, string chatType, [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50, long messageId = 0, bool loadOlder = false, bool loadBothDirections = false)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            var messages = await _messageService.GetChatMessages(chatId, chatType, userId, pageNumber, pageSize, messageId, loadOlder, loadBothDirections);
            return Ok(messages);
        }

        /// <summary>
        /// دریافت پیامهای سنجاق شده در گروه یا کانال مشخص
        /// </summary>
        /// <param name="classId"></param>
        /// <param name="chatType"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        [HttpGet("chatpinned/{classId}")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetChatPinnedMessages(int classId, string chatType, [FromQuery] int pageSize = 50)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            var messages = await _messageService.GetChatPinnedMessagesAsync(classId, chatType, userId, pageSize);
            return Ok(messages);
        }

        /// <summary>
        /// دریافت پیامهای گزارش شده در گروه یا کانال مشخص
        /// اگر اسکوپ برابر true  باشد حتی اگر کلاس ایدی هم ارسال شده باشه، باز هم همگی را بر میگرداند
        /// </summary>
        /// <param name="classId"></param>
        /// <param name="chatType">گروه یا کانال : ClassGroup || ChannelGroup </param>
        /// <param name="pageSize"></param>
        /// <param name="scope">همه چتها</param>
        /// <returns></returns>
        [HttpGet("chatreported/{classId}")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<ActionResult<IEnumerable<MessageFoulReportModelDto>>> GetReportedMessages(int classId, string chatType, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, bool scope = false)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            var messages = await _messageService.GetReportedMessagesAsync(classId, chatType, userId, pageNumber, pageSize, scope);
            return Ok(messages.OrderByDescending(o=>o.MessageId));
        }

        [HttpGet("{messageId}/readstatus")]
        public async Task<ActionResult<IEnumerable<MessageReadDto>>> GetMessageReadStatus(long messageId)
        {
            var readStatus = await _messageService.GetMessageReadStatusAsync(messageId);
            return Ok(readStatus);
        }

        /// <summary>
        /// استاد و مسول گروه میتوانند پیام را سنجاق یا از سنجاق خارج کنند
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{messageId}/pin")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel + "," + ConstRoles.Teacher)]
        public async Task<IActionResult> PinMessage(long messageId, [FromBody] PinMessageRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid request data.");

            if (request == null)
                return BadRequest("Request body is required.");

            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            try
            {
                await _broadcastService.NotifyGroupAboutPinAsync(userId, messageId, request.IsPinned);
                //  await _messageService.PinMessageAsync(userId, messageId, request.IsPinned);

                return Ok(new
                {
                    Message = request.IsPinned ? "Message pinned successfully." : "Message unpinned successfully.",
                    MessageId = messageId,
                    IsPinned = request.IsPinned
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "User {UserId} attempted to pin/unpin message {MessageId} without permission.", userId, messageId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogInformation(ex, "PinMessage: Message {MessageId} not found.", messageId);
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                _logger.LogInformation(ex, "PinMessage: invalid argument for message {MessageId}.", messageId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while pinning/unpinning message {MessageId} by user {UserId}.", messageId, userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        [HttpPost("hide")]
        public async Task<IActionResult> HideMessage(DeleteMessageRequestDto deleteMessageRequestModel)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            try
            {
                var hiddenMessageDto = await _messageService.HideMessageAsync(deleteMessageRequestModel.MessageId, userId,
               deleteMessageRequestModel.GroupId, deleteMessageRequestModel.GroupType, deleteMessageRequestModel.IsPortalMessage);

                if (hiddenMessageDto == null)
                {
                    return NotFound("Message not found or action not permitted.");
                }

                //TODO: این قسمت باید به BroadCast  منتقل بشه
                string? signalrGroup = null;
                if (hiddenMessageDto.MessageType == (byte)EnumMessageType.Group)
                {
                    signalrGroup = ConstChat.ClassGroup + hiddenMessageDto.ClassGroupId;
                }
                else if (hiddenMessageDto.MessageType == (byte)EnumMessageType.Channel)
                {
                    signalrGroup = ConstChat.ChannelGroup + hiddenMessageDto.ClassGroupId;
                }

                if (!string.IsNullOrEmpty(signalrGroup))
                {
                    await _hubContext.Clients.Group(signalrGroup).SendAsync("UserDeleteMessage", hiddenMessageDto.MessageId, true);
                    _logger.LogInformation("Broadcasted UserDeleteMessage for messageId {MessageId} to group {SignalRGroup}", hiddenMessageDto.MessageId, signalrGroup);
                }
                else
                {
                    _logger.LogWarning("HideMessage: Message {MessageId} was hidden, but no ClassGroupId or ChannelId found in DTO for SignalR group broadcast. Message might be private or context is missing.", deleteMessageRequestModel.MessageId);
                }

                return Ok(new { message = "Message hidden successfully.", messageId = hiddenMessageDto.MessageId });
            }
            catch (TimeLimitExceededException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    errorCode = "TIME_LIMIT_EXCEEDED",
                    message = ex.Message,
                    allowedMinutes = ex.AllowedMinutes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding message {MessageId}", deleteMessageRequestModel.MessageId);
                return StatusCode(500, new { success = false, message = "خطای سرور" });
            }
        }

        /// <summary>
        /// گزارش تخلف پیام
        /// </summary>
        /// <param name="messageId">ایدی پیام</param>
        /// <param name="request">توضیحات گزارش</param>
        /// <returns></returns>
        [HttpPost("{messageId}/report")]
        public async Task<ActionResult<MessageFoulReportDto>> ReportMessage(long messageId, [FromBody] MessageFoulReportDto request)
        {
            var reporterUserId = GetCurrentUserId();
            if (reporterUserId <= 0) return Unauthorized();
            try
            {
                var reportDto = await _messageService.ReportMessageAsync(messageId, reporterUserId, request.FoulDesc);
                return Ok(reportDto);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{messageId}/save")]
        public async Task<ActionResult> SaveMessage(long messageId)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            try
            {
                await _messageService.SaveMessageAsync(messageId, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("saved")]
        public async Task<ActionResult<IEnumerable<MessageSavedDto>>> GetSavedMessages()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            var savedMessages = await _messageService.GetSavedMessagesAsync(userId);
            return Ok(savedMessages);
        }

        [HttpDelete("saved/{messageSavedId}")]
        public async Task<IActionResult> DeleteSavedMessage(long messageSavedId)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            try
            {
                await _messageService.DeleteSavedMessageAsync(messageSavedId, userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("files/{messageFileId}/download")]
        public async Task<IActionResult> DownloadFile(long messageFileId)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var fileData = await _fileService.GetFileDataAsync(messageFileId, userId);

            if (fileData == null)
            {
                return NotFound("File not found or access denied.");
            }

            return File(fileData.ContentStream, fileData.ContentType, fileData.FileName);
        }

        //[HttpDelete("classgroup")]
        //public async Task<IActionResult> DeleteMessage(long senderUserId, int classGroupId, long messageId, string chatType)
        //{
        //    var userId = GetCurrentUserId();
        //    if (userId <= 0) return Unauthorized();
        //    try
        //    {
        //        await _messageService.DeleteChatMessageAsync(senderUserId, classGroupId, messageId, chatType);

        //        var signalrGroup = ConstChat.ClassGroup + classGroupId;
        //        await _hubContext.Clients.Group(signalrGroup).SendAsync("UserDeleteMessage", messageId, true);
        //        _logger.LogInformation("Broadcasted UserDeleteMessage for messageId {MessageId} to group {SignalRGroup} from dedicated delete endpoint.", messageId, signalrGroup);

        //        return NoContent();
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(ex.Message);
        //    }
        //}

        /// <summary>
        /// Get list of private chats for the current user
        /// </summary>
        [HttpGet("private-chats")]
        public async Task<ActionResult<IEnumerable<PrivateChatItemDto>>> GetPrivateChats()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            try
            {
                var privateChats = await _messageService.GetUserPrivateChatsAsync(userId);
                return Ok(privateChats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting private chats for user {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving private chats.");
            }
        }
    }
}

