using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services.Classes;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;



namespace Messenger.Services.Services
{
    public class MessageService : IMessageService
    {
        private readonly IEMessengerDbContext _context;

        //private readonly IFileService _fileService; // Dependency for file messages
        private readonly IFileManagementService _fileManagementService;
        private readonly IUserService _userService; // Dependency for user checks
        private readonly IChannelService _channelService; // Dependency for channel checks
        private readonly IClassGroupService _classGroupService; // Dependency for class group checks
        private readonly ILogger<MessageService> _logger;
        private readonly IRedisUnreadManage _redisUnreadManage;
        private readonly IPersonnelChatAccessService _personnelChatAccessService;
        private readonly TimeSettingOptions _timeSettings;

        // Constructor injection for dependencies
        public MessageService(IEMessengerDbContext context, IUserService userService,
            IChannelService channelService, IClassGroupService classGroupService,
            ILogger<MessageService> logger, IFileManagementService fileManagementService, IRedisUnreadManage redisUnreadManage,
            IPersonnelChatAccessService personnelChatAccessService, IOptions<TimeSettingOptions> timeSettings)
        {
            _context = context;
            //_fileService = fileService;
            _userService = userService;
            _channelService = channelService;
            _classGroupService = classGroupService;
            _logger = logger;
            _fileManagementService = fileManagementService;
            _redisUnreadManage = redisUnreadManage;
            _personnelChatAccessService = personnelChatAccessService;
            _timeSettings = timeSettings.Value;
        }



        /// <summary>
        /// just admin can send private message
        /// </summary>
        /// <param name="senderUserId"></param>
        /// <param name="receiverUserId"></param>
        /// <param name="messageText"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<MessageDto> SendPrivateMessageAsync(long senderUserId, long receiverUserId, string messageText,
            List<long>? fileIds = null, long? replyToMessageId = null, bool isPortalMessage = false)
        {
            //TODO : تکمیل بخش ذخیره فایل ها
            Console.WriteLine($"Attempting to send private message from {senderUserId} to {receiverUserId}");
            // 1. بررسی وجود ارسال کننده و دریافت کننده
            var sender = await _userService.GetUserByIdAsync(senderUserId);
            var receiver = await _userService.GetUserByIdAsync(receiverUserId);
            if (sender == null || receiver == null) throw new Exception("User not found");


            // 2. Create Message entity
            var messageEntity = new Message
            {
                OwnerId = receiverUserId, // For private messages, OwnerId is receiverUserId
                SenderUserId = senderUserId,
                MessageDateTime = DateTime.UtcNow,
                MessageType = (byte)EnumMessageType.Private,
                ReplyMessageId = replyToMessageId,
                IsSystemMessage = isPortalMessage
            };
            _context.Messages.Add(messageEntity);

            // 3. Create MessageText entity
            var messageTextEntity = new MessageText
            {
                MessageId = messageEntity.MessageId, // After saving messageEntity
                MessageTxt = messageText
            };
            _context.MessageTexts.Add(messageTextEntity);

            await _context.SaveChangesAsync();

            // Map to DTO and return
            return new MessageDto
            {
                MessageId = messageEntity.MessageId,
                SenderUserId = senderUserId,
                MessageDateTime = messageEntity.MessageDateTime,
                MessageType = messageEntity.MessageType,
                ReceiverUserId = receiverUserId
                //MessageText = messageTextEntity.MessageTxt
            };
        }


        /// <summary>
        /// ارسال پیام به گروه یا کانال
        /// </summary>
        /// <param name="senderUserId"></param>
        /// <param name="chatId"></param>
        /// <param name="chatType"></param>
        /// <param name="messageText"></param>
        /// <param name="fileIds"></param>
        /// <param name="replyToMessageId"></param>
        /// <param name="isPortalMessage">اگه ارسال کننده پرتال بود</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<MessageDto> SendGroupMessageAsync(long senderUserId, long chatId, string chatType, string messageText,
             List<long>? fileIds = null, long? replyToMessageId = null, bool isPin = false, bool isPortalMessage = false)
        {
            _logger.LogInformation($"Attempting to send class group message from {senderUserId} to class {chatId}");

            // بررسی اینکه ارسال کننده عضو این گروه است یا خیر
            //  فعلا تا تصمیم گیری نهایی این رو معلق میکنیم
            bool hasAccess = false;
            //if (isPortalMessage)
            //{
            //    hasAccess = await _personnelChatAccessService.HasAccessToSendToChatAsync(senderUserId, chatId, chatType);
            //}
            //else
            //{
            hasAccess = chatType == ConstChat.ClassGroupType ?
           await _classGroupService.IsUserMemberOfClassGroupAsync(senderUserId, chatId)
           : await _channelService.IsUserMemberOfChannelAsync(senderUserId, chatId);
            //}
            // بدست اوردن نقش کاربر
            var user = await _userService.GetUserByIdAsync(senderUserId);

            if (!hasAccess)
            {               
                if (user == null)
                {
                    throw new Exception("User is not a member of this class group");
                }
                // اگر نقش ادمین یا پرسنل بود اجازه ارسال پیام بده
                if (user.RoleName != ConstRoles.Manager && user.RoleName != ConstRoles.Personel)
                {
                    throw new Exception("User is not a member of this class group");
                }
            }

            // شروع تراکنش
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                //تعین نوع چت
                var chatTypeDetected = chatType switch
                {
                    ConstChat.ClassGroupType => (byte)EnumMessageType.Group,
                    ConstChat.ChannelGroupType => (byte)EnumMessageType.Channel,
                    ConstChat.PrivateType => (byte)EnumMessageType.Private,
                    _ => throw new ArgumentException("Invalid chat type", nameof(chatType))
                };

                // ایجاد پیام
                var messageEntity = new Message
                {
                    OwnerId = chatId,
                    SenderUserId = senderUserId,
                    MessageDateTime = DateTime.UtcNow,
                    MessageType = chatTypeDetected,
                    ReplyMessageId = replyToMessageId,
                    IsPin = isPin,
                    IsSystemMessage = isPortalMessage
                };


                if (isPin)
                {
                    messageEntity.PinnedByUserId = senderUserId;
                    messageEntity.PinnedAt = DateTime.UtcNow;
                }

                _context.Messages.Add(messageEntity);
                await _context.SaveChangesAsync(); // تا MessageId تولید شود

                // افزودن متن پیام (در صورت وجود)
                MessageText? messageTextEntity = null;
                if (!string.IsNullOrEmpty(messageText))
                {
                    messageTextEntity = new MessageText
                    {
                        MessageTxt = messageText,
                        MessageId = messageEntity.MessageId // بعد از ذخیره پیام
                    };
                    _context.MessageTexts.Add(messageTextEntity);
                }

                await _context.SaveChangesAsync();

                // Attach pre-uploaded files to the message
                if (fileIds != null && fileIds.Any())
                {
                    foreach (var fileId in fileIds)
                    {
                        var messageFile = await _context.MessageFiles.FirstOrDefaultAsync(f => f.MessageFileId == fileId && f.MessageId == null);

                        if (messageFile == null)
                            throw new Exception($"File with ID {fileId} not found or already attached.");

                        // **اتصال فایل به پیام ارسالی**
                        messageFile.MessageId = messageEntity.MessageId;
                        _context.MessageFiles.Update(messageFile);
                    }
                }

                // ذخیره تمام تغییرات
                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // تأیید تراکنش

                //var user = await _userService.GetUserByIdAsync(senderUserId);
                var replyMessage = replyToMessageId != null ? await GetMessageByIdAsync(senderUserId, replyToMessageId.Value) : null;

                var fileAttachments = fileIds != null ? await GetMessageFiles(messageEntity.MessageId) : null;

                //var fileExtention = fileIds !=null ? messageEntity.MessageFiles.

                var resultModel = new MessageDto
                {
                    MessageId = messageEntity.MessageId,
                    SenderUserId = senderUserId,
                    MessageDateTime = messageEntity.MessageDateTime,
                    MessageType = messageEntity.MessageType,
                    ReplyMessageId = replyToMessageId,
                    OwnerId = chatId,
                    IsSystemMessage = isPortalMessage,
                    MessageText = messageTextEntity != null ? new MessageTextDto
                    {
                        MessageTextId = messageTextEntity.MessageTextId,
                        MessageTxt = messageTextEntity.MessageTxt
                    } : null,
                    SenderUser = new UserDto
                    {
                        UserId = senderUserId,
                        NameFamily = user.NameFamily,
                        ProfilePicName = user.ProfilePicName,
                    },
                    ReplyMessage = replyMessage,
                    //MessageFiles = fileAttachments
                };

                if (fileIds != null && messageEntity.MessageFiles.Any())
                {
                    resultModel.MessageFiles = fileAttachments;

                }

                return resultModel;

                //  await _apiHubContext.Clients.Group(request.ClassId.ToString()).SendAsync("ReceiveMessage", messageDto);

            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(); // در صورت خطا، برگرداندن تغییرات
                _logger.LogError(ex, "Error occurred while sending class group message.");
                throw; // مجدداً پرتاب خطا
            }
        }



        /// <summary>
        /// ارسال پیام به همه گروه ها یا کانال ها یا افراد
        /// </summary>
        /// <param name="messageType">این تایپ مشخص میکنه به همه دانشجو ها یا اساتید یا پرسنل باشد</param>
        /// <param name="messageText">متن پیام</param>
        /// <param name="isPin">آیا پیام پین شده باشد</param>
        /// <returns>اطلاعات پیام ذخیره شده</returns>
        /// <exception cref="InvalidOperationException">اگر خطایی در ذخیره یا ارسال پیام رخ دهد</exception>
        public async Task<MessageDto> SendMessageToAllAsync(long senderUserId, EnumMessageType messageType,
            string messageText, bool isPin = true, bool isPortalMessage = true)
        {
            _logger.LogInformation($"Attempting to send message from portal account to messageType {messageType}");

            // بدست اوردن نقش کاربر
            var user = await _userService.GetUserByIdAsync(senderUserId);
            if (user == null)
            {
                throw new Exception("User is not a member of this class group");
            }
            // اگر نقش ادمین یا مدرس بود اجازه ارسال پیام بده
            if (user.RoleName != ConstRoles.Manager && user.RoleName != ConstRoles.Personel)
            {
                throw new Exception("User is not a member of this class group");
            }


            // شروع تراکنش
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {

                // ایجاد پیام
                var messageEntity = new Message
                {
                    SenderUserId = senderUserId,
                    MessageDateTime = DateTime.UtcNow,
                    MessageType = (byte)messageType,
                    IsPin = isPin,
                    IsSystemMessage = isPortalMessage
                };


                if (isPin)
                {
                    messageEntity.PinnedAt = DateTime.UtcNow;
                    messageEntity.PinnedByUserId = senderUserId;
                }

                _context.Messages.Add(messageEntity);
                await _context.SaveChangesAsync(); // تا MessageId تولید شود

                // افزودن متن پیام (در صورت وجود)
                MessageText? messageTextEntity = null;
                if (!string.IsNullOrEmpty(messageText))
                {
                    messageTextEntity = new MessageText
                    {
                        MessageTxt = messageText,
                        MessageId = messageEntity.MessageId // بعد از ذخیره پیام
                    };
                    _context.MessageTexts.Add(messageTextEntity);
                }

                // ذخیره تمام تغییرات
                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // تأیید تراکنش

                var messageSavedModel = new MessageDto
                {
                    MessageId = messageEntity.MessageId,
                    SenderUserId = 0,
                    MessageDateTime = messageEntity.MessageDateTime,
                    MessageType = messageEntity.MessageType,
                    MessageText = messageTextEntity != null ? new MessageTextDto
                    {
                        MessageTextId = messageTextEntity.MessageTextId,
                        MessageTxt = messageTextEntity.MessageTxt
                    } : null,
                    SenderUser = new UserDto
                    {
                        UserId = 0,
                        NameFamily = "Portal",
                    },
                };

                return messageSavedModel;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(); // در صورت خطا، برگرداندن تغییرات
                _logger.LogError(ex, "Error occurred while sending broadcast message: {Error}", ex.Message);
                throw new InvalidOperationException("Failed to send broadcast message", ex); // Wrap with more user-friendly message
            }
        }



        private async Task<IEnumerable<MessageFileDto>> GetMessageFiles(long messageId)
        {
            var files = await _context.MessageFiles
                .Include(m => m.FileExtension).Where(w => w.MessageId == messageId).ToListAsync();

            if (files == null)
            {
                return null;
            }

            var resModel = files.Select(file => new MessageFileDto
            {
                FileExtension = new FileExtensionDto
                {
                    Extension = file.FileExtension.Extension,
                    FontAwesome = file.FileExtension.FontAwesome,
                    Type = file.FileExtension.Type
                },
                CreateDate = file.CreateDate,
                FileName = file.FileName,
                OriginalFileName = file.OriginalFileName,
                FileSize = file.FileSize,
                FilePath = file.FilePath,
                FileThumbPath = file.FileThumbPath,
                MessageFileId = file.MessageFileId
            });

            return resModel;
        }


        /// <summary>
        /// بررسی دسترسی کاربر به چت
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="chatId"></param>
        /// <param name="chatType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<bool> AccessToChat(long userId, long chatId, string chatType)
        {
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found!");

            if (user.RoleName != ConstRoles.Manager && user.RoleName != ConstRoles.Personel)
            {
                if (chatType == ConstChat.ClassGroupType)
                {
                    var isMember = await _classGroupService.IsUserMemberOfClassGroupAsync(userId, chatId);
                    if (!isMember)
                    {
                        throw new Exception("User is not a member of this class group");
                    }
                }
                else if (chatType == ConstChat.ChannelGroupType)
                {
                    var isMember = await _channelService.IsUserMemberOfChannelAsync(userId, chatId);
                    if (!isMember)
                    {
                        throw new Exception("User is not a member of this class group");
                    }
                }
                else if (chatType == ConstChat.PrivateType)
                {
                    // اگر کاربر نقش پرسنل داشت یا نقش مدیر داشت مجاز است
                    //اگر غیر از این بود باید بررسی بشه مدیر یا پرسنل بهش پیام داده یا نه

                    if (user.RoleName != ConstRoles.Manager && user.RoleName != ConstRoles.Personel)
                    {
                        // کاربر غیر از مدیر و پرسنل است

                        // chatId  همان ایدی پرسنل میباشد

                        //بررسی میشه ایا مدیر یا پرسنل بهش پیام داده یا نه
                        var message = await _context.Messages.FirstOrDefaultAsync(f => f.SenderUserId == chatId && f.OwnerId == userId && !f.IsHidden);
                        if (true)
                            throw new Exception($"User can not message to chatId: {chatId}");
                    }
                }
                else
                {
                    throw new Exception("Chat Type not found");
                }
            }
            else if (user.RoleName == ConstRoles.Personel)
            {
                //بررسی دسترسی پرسنل به این گروه
                //TODO : در یک جلسه مشخص بشه
            }
            return true;
        }


        /// <summary>
        /// دسترسی به پیام
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<bool> HasAccessToMessage(long userId, Message message)
        {
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                throw new Exception("User not found!");
            }


            if (user.RoleName != ConstRoles.Manager && user.RoleName != ConstRoles.Personel)
            {
                //--اگر نقش غیر از این بود باید سطح دسترسی بررسی بشه

                var messagetype = message.MessageType;
                if (messagetype == (byte)EnumMessageType.Group)
                {
                    //--بررسی دسترسی به گروه
                    var isMember = await _classGroupService.IsUserMemberOfClassGroupAsync(userId, message.OwnerId);
                    if (!isMember)
                    {
                        throw new Exception("User is not a member of this class group");
                    }
                }
                else if (messagetype == (byte)EnumMessageType.Channel)
                {
                    //بررسی دسترسی به کانال
                    var isMember = await _channelService.IsUserMemberOfChannelAsync(userId, message.OwnerId);
                    if (!isMember)
                    {
                        throw new Exception("User is not a member of this channel");
                    }
                }
                else if (messagetype == (byte)EnumMessageType.Private)
                {

                    if (message.SenderUserId != userId && message.OwnerId != userId)
                    {
                        throw new Exception("User is not a member of this private chat");
                    }
                }
                else if (messagetype == (byte)EnumMessageType.AllTeachers)
                {
                    if (user.RoleName != ConstRoles.Teacher)
                    {
                        throw new Exception("User is not a teacher");
                    }
                }
                else if (messagetype == (byte)EnumMessageType.AllStudents)
                {
                    if (user.RoleName != ConstRoles.Student)
                    {
                        throw new Exception("User is not a student");
                    }
                }
                else if (messagetype == (byte)EnumMessageType.AllPersonel)
                {
                    if (user.RoleName != ConstRoles.Personel)
                    {
                        throw new Exception("User is not a personnel");
                    }
                }
            }

            return true;
        }


        public async Task<MessageDto> GetMessageByIdAsync(long userId, long messageId)
        {
            var message = await _context.Messages
                .Include(m => m.MessageTexts)
                .Include(m => m.MessageFiles)
                    .ThenInclude(mf => mf.FileExtension)
                .Include(m => m.ReplyMessage)
                    .ThenInclude(rm => rm.MessageTexts)
                .FirstOrDefaultAsync(m => m.MessageId == messageId);

            if (message == null)
                throw new Exception("Message not found");

            //has access to message
            var hasAccess = HasAccessToMessage(userId, message);

            var sender = await _userService.GetUserByIdAsync(message.SenderUserId);

            var chatId = message.OwnerId;

            var fileAttachements = message.MessageFiles != null ? await GetMessageFiles(messageId) : null;

            return new MessageDto
            {
                MessageId = message.MessageId,
                SenderUserId = message.SenderUserId,
                MessageDateTime = message.MessageDateTime,
                IsPin = message.IsPin,
                PinnedByUserId = message.PinnedByUserId,
                PinnedAt = message.PinnedAt,
                // LastEditDateTime = message.LastEditDateTime,
                MessageType = message.MessageType,
                ReplyMessageId = message.ReplyMessageId,
                OwnerId = message.OwnerId,
                MessageText = message.MessageTexts.FirstOrDefault() != null ? new MessageTextDto
                {
                    MessageTextId = message.MessageTexts.First().MessageTextId,
                    MessageTxt = message.MessageTexts.First().MessageTxt
                } : null,
                MessageFiles = fileAttachements,
                SenderUser = new UserDto
                {
                    UserId = sender.UserId,
                    NameFamily = sender.NameFamily,
                    ProfilePicName = sender.ProfilePicName
                },
                ReplyMessage = message.ReplyMessage != null ? new MessageDto
                {
                    MessageId = message.ReplyMessage.MessageId,
                    SenderUserId = message.ReplyMessage.SenderUserId,
                    MessageDateTime = message.ReplyMessage.MessageDateTime,
                    MessageText = message.ReplyMessage.MessageTexts.FirstOrDefault() != null ? new MessageTextDto
                    {
                        MessageTextId = message.ReplyMessage.MessageTexts.First().MessageTextId,
                        MessageTxt = message.ReplyMessage.MessageTexts.First().MessageTxt
                    } : null
                } : null
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId1">Sender</param>
        /// <param name="userId2">Getter</param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public async Task<IEnumerable<MessageDto>> GetPrivateMessagesAsync(long userId1, long userId2, int pageNumber, int pageSize) // Assuming userId1 is currentUserId
        {
            // Console.WriteLine($"Getting private messages between {userId1} and {userId2}"); // Original log
            _logger.LogInformation($"Getting private messages between {userId1} and {userId2}. Requester assumed: {userId1}");
            // Query messages where (Sender=userId1 AND Getter=userId2) OR (Sender=userId2 AND Getter=userId1)
            // Apply pagination
            var messages = await _context.Messages
                .Include(m => m.MessageTexts)
                .Include(m => m.SenderUser)
                .Include(m => m.MessageReads) // Added include for MessageReads
                .Include(m => m.ReplyMessage)
                    .ThenInclude(rm => rm.MessageTexts)
                .Include(m => m.ReplyMessage)
                    .ThenInclude(rm => rm.SenderUser)
                .Include(m => m.MessageFiles).ThenInclude(mf => mf.FileExtension)
                .Where(m => m.MessageType == (byte)EnumMessageType.Private && !m.IsSystemMessage && !m.IsHidden &&
                            ((m.SenderUserId == userId1 && m.OwnerId == userId2) ||
                             (m.SenderUserId == userId2 && m.OwnerId == userId1)))
                .OrderByDescending(m => m.MessageDateTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Map to DTOs
            return new List<MessageDto>(messages.Select(m => new MessageDto
            {
                MessageId = m.MessageId,
                SenderUserId = m.SenderUserId,
                MessageDateTime = m.MessageDateTime,
                MessageType = m.MessageType,
                IsHidden = m.IsHidden,
                IsPin = m.IsPin,
                MessageFiles = m.MessageFiles?.Select(mf => new MessageFileDto
                {
                    MessageFileId = mf.MessageFileId,
                    FileName = mf.FileName,
                    OriginalFileName = mf.OriginalFileName,
                    FileSize = mf.FileSize,
                    CreateDate = mf.CreateDate,
                    FilePath = mf.FilePath,
                    FileThumbPath = mf.FileThumbPath,
                    FileExtension = new FileExtensionDto
                    {
                        Comment = mf.FileExtension.Comment,
                        Extension = mf.FileExtension.Extension,
                        FileExtensionId = mf.FileExtension.FileExtensionId,
                        FontAwesome = mf.FileExtension.FontAwesome,
                        Type = mf.FileExtension.Type
                    }
                }).ToList(),
                MessageText = m.MessageTexts?.Select(mt => new MessageTextDto
                {
                    MessageTextId = mt.MessageTextId,
                    MessageTxt = mt.MessageTxt
                }).FirstOrDefault(),
                ReplyMessageId = m.ReplyMessage?.MessageId,
                ReplyMessage = m.ReplyMessage == null ? null : new MessageDto
                {
                    MessageId = m.ReplyMessage.MessageId,
                    SenderUserId = m.ReplyMessage.SenderUserId,
                    MessageDateTime = m.ReplyMessage.MessageDateTime,
                    MessageType = m.ReplyMessage.MessageType,
                    IsHidden = m.ReplyMessage.IsHidden,
                    IsPin = m.ReplyMessage.IsPin,
                    MessageText = m.ReplyMessage.MessageTexts?.Select(mt => new MessageTextDto
                    {
                        MessageTextId = mt.MessageTextId,
                        MessageTxt = mt.MessageTxt
                    }).FirstOrDefault(),
                    SenderUser = new UserDto
                    {
                        UserId = m.ReplyMessage.SenderUser.UserId,
                        NameFamily = m.ReplyMessage.SenderUser.NameFamily,
                        ProfilePicName = m.ReplyMessage.SenderUser.ProfilePicName
                    }
                },
                SenderUser = new UserDto
                {
                    UserId = m.SenderUserId,
                    DeptName = m.SenderUser.DeptName,
                    NameFamily = m.SenderUser.NameFamily,
                    ProfilePicName = m.SenderUser.ProfilePicName,
                    RoleFaName = m.SenderUser.RoleFaName,
                    RoleName = m.SenderUser.RoleName,
                },
                IsReadByCurrentUser = m.MessageReads.Any(r => r.UserId == userId1 && r.MessageId == m.MessageId), // Assuming userId1 is currentUserId
                IsReadByAnyRecipient = (m.SenderUserId == userId1) && (m.MessageReads != null && m.MessageReads.Any(mr => mr.UserId != userId1)) // Assuming userId1 is currentUserId
            }));

        }

        public async Task<IEnumerable<MessageDto>> GetChannelMessagesAsync(long channelId, long currentUserId, int pageNumber, int pageSize) // Added currentUserId
        {
            // Console.WriteLine($"Getting messages for channel {channelId}"); // Original log
            _logger.LogInformation($"Getting messages for channel {channelId} for user {currentUserId}");
            // Query messages linked to channelId via ChannelMessages table
            // Apply pagination
            var messages = await _context.Messages
                .Include(m => m.MessageTexts)
                .Include(m => m.SenderUser)
                .Include(m => m.MessageReads) // Added include for MessageReads
                .Include(m => m.ReplyMessage)
                    .ThenInclude(rm => rm.MessageTexts)
                .Include(m => m.ReplyMessage)
                    .ThenInclude(rm => rm.SenderUser)
                .Include(m => m.MessageFiles).ThenInclude(mf => mf.FileExtension)
                .Where(m => m.OwnerId == channelId && m.IsHidden == false)
                .OrderByDescending(m => m.MessageDateTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Map to DTOs
            return new List<MessageDto>(messages.Select(m => new MessageDto
            {
                MessageId = m.MessageId,
                SenderUserId = m.SenderUserId,
                MessageDateTime = m.MessageDateTime,
                MessageType = m.MessageType,
                IsHidden = m.IsHidden,
                IsPin = m.IsPin,
                MessageFiles = m.MessageFiles?.Select(mf => new MessageFileDto
                {
                    MessageFileId = mf.MessageFileId,
                    OriginalFileName = mf.OriginalFileName,
                    FileName = mf.FileName,
                    FileSize = mf.FileSize,
                    CreateDate = mf.CreateDate,
                    FilePath = mf.FilePath,
                    FileThumbPath = mf.FileThumbPath,
                    FileExtension = new FileExtensionDto
                    {
                        Comment = mf.FileExtension.Comment,
                        Extension = mf.FileExtension.Extension,
                        FileExtensionId = mf.FileExtension.FileExtensionId,
                        FontAwesome = mf.FileExtension.FontAwesome,
                        Type = mf.FileExtension.Type
                    }
                }).ToList(),
                MessageText = m.MessageTexts?.Select(mt => new MessageTextDto
                {
                    MessageTextId = mt.MessageTextId,
                    MessageTxt = mt.MessageTxt
                }).FirstOrDefault(),
                ReplyMessageId = m.ReplyMessage?.MessageId,
                ReplyMessage = m.ReplyMessage == null ? null : new MessageDto
                {
                    MessageId = m.ReplyMessage.MessageId,
                    SenderUserId = m.ReplyMessage.SenderUserId,
                    MessageDateTime = m.ReplyMessage.MessageDateTime,
                    MessageType = m.ReplyMessage.MessageType,
                    IsHidden = m.ReplyMessage.IsHidden,
                    IsPin = m.ReplyMessage.IsPin,
                    MessageText = m.ReplyMessage.MessageTexts?.Select(mt => new MessageTextDto
                    {
                        MessageTextId = mt.MessageTextId,
                        MessageTxt = mt.MessageTxt
                    }).FirstOrDefault(),
                    SenderUser = new UserDto
                    {
                        UserId = m.ReplyMessage.SenderUser.UserId,
                        NameFamily = m.ReplyMessage.SenderUser.NameFamily,
                        ProfilePicName = m.ReplyMessage.SenderUser.ProfilePicName
                    }
                },
                SenderUser = new UserDto
                {
                    UserId = m.SenderUserId,
                    DeptName = m.SenderUser.DeptName,
                    NameFamily = m.SenderUser.NameFamily,
                    ProfilePicName = m.SenderUser.ProfilePicName,
                    RoleFaName = m.SenderUser.RoleFaName,
                    RoleName = m.SenderUser.RoleName,
                },
                IsReadByCurrentUser = m.MessageReads.Any(r => r.UserId == currentUserId && r.MessageId == m.MessageId),
                IsReadByAnyRecipient = (m.SenderUserId == currentUserId) && (m.MessageReads != null && m.MessageReads.Any(mr => mr.UserId != currentUserId))
            }));
        }


        //TODO عملیات دریافت پیام برای کانال هم در همین متد قرار بگیرد
        /// <summary>
        /// دریافت پیامهای گروه به تعداد 50 عدد
        /// </summary>
        /// <param name="chatId"></param>
        /// <param name="currentUserId"></param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <param name="messageId">برای صفحه بندی کاربرد دارد و ایدی قدیمی ترین پیامی که کاربر جاری دریافت کرده  را نمایش میدهد</param>
        /// <returns></returns>
        public async Task<IEnumerable<MessageDto>> GetChatMessages(long chatId,
            string chatType, long currentUserId, int pageNumber, int pageSize,
            long messageId, bool loadOlder = false, bool loadBothDirections = false)
        {
            try
            {
                // Access check (skip for Private since OwnerId ensures access)
                if (chatType != ConstChat.PrivateType)
                {
                    var hasAccess = chatType == ConstChat.ClassGroupType ?
                        await _classGroupService.IsUserMemberOfClassGroupAsync(currentUserId, chatId)
                        : await _channelService.IsUserMemberOfChannelAsync(currentUserId, chatId);

                    if (!hasAccess)
                    {
                        _logger.LogWarning($"User {currentUserId} does not have access to chat {chatId}");
                        return new List<MessageDto>();
                    }
                }

                _logger.LogInformation($"Getting messages for chat {chatId} (type: {chatType}) for user {currentUserId}");

                var chatEnumType = chatType switch
                {
                    ConstChat.ClassGroupType => EnumMessageType.Group,
                    ConstChat.ChannelGroupType => EnumMessageType.Channel,
                    ConstChat.PrivateType => EnumMessageType.Private,
                    _ => throw new ArgumentException($"Unsupported chat type: {chatType}")
                };

                // For Private chats, chatId is the other user's ID, so pass currentUserId as well
                var baseQuery = chatType == ConstChat.PrivateType 
                    ? GetMessagesQuery(chatId, chatEnumType, currentUserId)
                    : GetMessagesQuery(chatId, chatEnumType);
                if (baseQuery == null)
                    return new List<MessageDto>();

                // Case 1: Load messages around a target (for pinned messages)
                if (loadBothDirections && messageId > 0)
                {
                    var test = await baseQuery.OrderByDescending(o => o.MessageId).Where(w => w.MessageId >= messageId).ToListAsync();

                    var beforeMessages = await baseQuery
                        .OrderByDescending(m => m.MessageId)
                        .Where(m => m.MessageId < messageId)
                        .Take(25)
                        .ToListAsync();

                    var afterMessages = await baseQuery
                        .OrderBy(m => m.MessageId)
                        .Where(m => m.MessageId >= messageId)
                        .Take(20)
                        .ToListAsync();

                    var combined = beforeMessages.Concat(afterMessages).OrderBy(m => m.MessageId).ToList();
                    return MapMessagesToDto(combined, currentUserId);
                }

                // Case 2: Load older messages
                if (loadOlder && messageId > 0)
                {
                    var olderMessages = await baseQuery
                        .Where(m => m.MessageId < messageId)
                        .OrderByDescending(m => m.MessageId)
                        .Take(pageSize)
                        .ToListAsync();
                    return MapMessagesToDto(olderMessages.OrderBy(m => m.MessageId).ToList(), currentUserId);
                }

                // Case 3: Load newer messages
                if (!loadOlder && messageId > 0)
                {
                    var newerMessages = await baseQuery
                        .Where(m => m.MessageId > messageId)
                        .OrderBy(m => m.MessageId)
                        .Take(pageSize)
                        .ToListAsync();
                    return MapMessagesToDto(newerMessages, currentUserId);
                }

                // Case 4: Initial load
                long lastReadMessageId = await _redisUnreadManage.GetLastReadMessageIdAsync(currentUserId, chatId, chatType);
                if (chatType == ConstChat.ChannelGroupType)
                {
                    lastReadMessageId = 0;
                }

                if (lastReadMessageId > 0)
                {
                    var beforeMessages = await baseQuery
                        .OrderByDescending(m => m.MessageId)
                        .Where(m => m.MessageId < lastReadMessageId)
                        .Take(30)
                        .ToListAsync();

                    var afterMessages = await baseQuery
                        .OrderByDescending(m => m.MessageId)
                        .Where(m => m.MessageId >= lastReadMessageId)
                        .Take(20)
                        .ToListAsync();

                    var combined = beforeMessages.Concat(afterMessages).OrderBy(m => m.MessageId).ToList();
                    return MapMessagesToDto(combined, currentUserId);
                }

                // Default: Load recent messages
                var recentMessages = await baseQuery
                    .OrderByDescending(m => m.MessageId)
                    .Take(50)
                    .ToListAsync();
                return MapMessagesToDto(recentMessages.OrderBy(m => m.MessageId).ToList(), currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetClassGroupMessagesAsync for classId {classId}, userId {userId}", chatId, currentUserId);
                throw;
            }
        }


        public async Task<IEnumerable<MessageDto>> GetChatPinnedMessagesAsync(
            long chatId,
            string chatType,
            long currentUserId,
            int pageSize)
        {
            try
            {
                // Access check
                var hasAccess = chatType == ConstChat.ClassGroupType ?
                    await _classGroupService.IsUserMemberOfClassGroupAsync(currentUserId, chatId)
                    : await _channelService.IsUserMemberOfChannelAsync(currentUserId, chatId);
                if (!hasAccess)
                {
                    _logger.LogWarning($"User {currentUserId} does not have access to class group {chatId}");
                    return new List<MessageDto>();
                }


                _logger.LogInformation($"Getting messages for class group {chatId} for user {currentUserId}");

                var baseQuery = chatType == ConstChat.ClassGroupType ?
                    GetMessagesQuery(chatId, EnumMessageType.Group)
                    : GetMessagesQuery(chatId, EnumMessageType.Channel);

                if (baseQuery == null)
                    return new List<MessageDto>();


                var recentMessages = await baseQuery
                    .OrderByDescending(m => m.MessageId)
                    .Where(m => m.IsPin && m.IsHidden == false)
                    .Take(50)
                    .OrderBy(m => m.MessageId)
                    .ToListAsync();

                return MapMessagesToDto(recentMessages, currentUserId);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetClassGroupMessagesAsync for classId {classId}, userId {userId}", chatId, currentUserId);
                throw;
            }
        }


        public async Task<IEnumerable<MessageFoulReportModelDto>> GetReportedMessagesAsync(
            long chatId,
            string chatType,
            long currentUserId,
            int pageNumber,
            int pageSize,
            bool scope = false)
        {
            try
            {
                //TODO: بررسی سطوح دسترسی بعدا مشخص بشه
                if (scope)
                {
                    var user = await _userService.GetUserByIdAsync(currentUserId);
                    if (user == null)
                    {
                        _logger.LogWarning($"User {currentUserId} not found");
                        return new List<MessageFoulReportModelDto>();
                    }

                    // Only Manager or personnel with send-access may list reports across all chats
                    if (user.RoleName != ConstRoles.Manager && user.RoleName != ConstRoles.Personel)
                    {
                        _logger.LogWarning($"User {currentUserId} ({user.RoleName}) not authorized to query reported messages across all chats");
                        return new List<MessageFoulReportModelDto>();
                    }

                    // Personnel: require at least one PersonelChatAccess with send permission (mirror existing per-chat logic)
                    //if (user.RoleName == ConstRoles.Personel)
                    //{
                    //    var hasAnyAccess = await _context.PersonelChatAccesses
                    //        .AnyAsync(p => p.PersonelId == currentUserId && p.AccessSendMessageInChat);
                    //    if (!hasAnyAccess)
                    //    {
                    //        _logger.LogWarning($"Personnel User {currentUserId} does not have global access to query reported messages");
                    //        return new List<MessageFoulReportModelDto>();
                    //    }
                    //}

                    _logger.LogInformation($"Getting reported messages for ALL {chatType} chats for user {currentUserId}");

                    var baseQueryAll = GetGlobalMessagesQuery(chatType);

                    // normalize paging params
                    pageNumber = Math.Max(1, pageNumber);
                    pageSize = Math.Max(1, pageSize);

                    var reportedMessagesPage = await baseQueryAll
                        .Where(m => m.MessageFoulReports != null && m.MessageFoulReports.Count > 0)
                        .OrderByDescending(m => m.MessageId)
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    var recentMessages = reportedMessagesPage.OrderBy(m => m.MessageId).ToList();
                    var messageDtos = MapMessagesForReportedMessages(recentMessages, currentUserId);

                    var result = new List<MessageFoulReportModelDto>();
                    foreach (var message in recentMessages)
                    {
                        var messageInfo = messageDtos.FirstOrDefault(m => m.MessageId == message.MessageId);
                        var messageReports = message.MessageFoulReports.Select(r => new MessageFoulReportModelDto
                        {
                            MessageFoulReportId = r.MessageFoulReportId,
                            MessageId = r.MessageId,
                            FoulReporterUserId = r.FoulReporterUserId,
                            FoulDesc = r.FoulDesc,
                            FoulReportDateTime = r.FoulReportDateTime,
                            MessageDto = messageInfo
                        }).ToList();

                        result.AddRange(messageReports);
                    }

                    return result;
                }

                // Default behaviour: single chat (existing logic)
                var hasAccess = chatType == ConstChat.ClassGroupType ?
                    await _classGroupService.IsUserMemberOfClassGroupAsync(currentUserId, chatId)
                    : await _channelService.IsUserMemberOfChannelAsync(currentUserId, chatId);
                if (!hasAccess)
                {
                    // اگر نقش کاربر مدیر یا پرسنل بود اجازه بده
                    var user = await _userService.GetUserByIdAsync(currentUserId);
                    if (user == null)
                    {
                        _logger.LogWarning($"User {currentUserId} not found");
                        return new List<MessageFoulReportModelDto>();
                    }

                    if (user.RoleName != ConstRoles.Manager && user.RoleName != ConstRoles.Personel)
                    {
                        _logger.LogWarning($"User {currentUserId} ({user.RoleName}) not authorized to query reported messages across all chats");
                        return new List<MessageFoulReportModelDto>();
                    }

                    //TODO: اگر نقش کاربر پرسنل بود باید دسترسی ایشان به این چت بررسی بشه
                    //if (user.RoleName == ConstRoles.Personel)
                    //{
                    //    var canAccess = await _context.PersonelChatAccesses.FirstOrDefaultAsync(f => f.PersonelId == currentUserId &&
                    //        f.AccessSendMessageInChat);

                    //    if (canAccess == null)
                    //    {
                    //        _logger.LogWarning($"Personnel User {currentUserId} does not have access to channel {classId}");
                    //        return new List<MessageFoulReportModelDto>();
                    //    }
                    //}
                }

                _logger.LogInformation($"Getting reported messages for class/group {chatId} for user {currentUserId}");

                var baseQuery = chatType == ConstChat.ClassGroupType
                    ? GetMessagesQuery(chatId, EnumMessageType.Group)
                    : GetMessagesQuery(chatId, EnumMessageType.Channel);

                if (baseQuery == null)
                    return new List<MessageFoulReportModelDto>();

                // normalize paging params
                pageNumber = Math.Max(1, pageNumber);
                pageSize = Math.Max(1, pageSize);

                // Query messages that have foul reports, apply server-side paging on messages
                var reportedMessagesPageLocal = await baseQuery
                    .Where(m => m.MessageFoulReports != null && m.MessageFoulReports.Count > 0)
                    .OrderByDescending(m => m.MessageId) // newest first for consistent paging
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Order the page ascending for display consistency (oldest -> newest inside page)
                var recentMessagesLocal = reportedMessagesPageLocal.OrderBy(m => m.MessageId).ToList();

                var messageDtosLocal = MapMessagesToDto(recentMessagesLocal, currentUserId);

                var resultLocal = new List<MessageFoulReportModelDto>();
                foreach (var message in recentMessagesLocal)
                {
                    var messageInfo = messageDtosLocal.FirstOrDefault(m => m.MessageId == message.MessageId);
                    var messageReports = message.MessageFoulReports.Select(r => new MessageFoulReportModelDto
                    {
                        MessageFoulReportId = r.MessageFoulReportId,
                        MessageId = r.MessageId,
                        FoulReporterUserId = r.FoulReporterUserId,
                        FoulDesc = r.FoulDesc,
                        FoulReportDateTime = r.FoulReportDateTime,
                        MessageDto = messageInfo
                    }).ToList();

                    resultLocal.AddRange(messageReports);
                }

                return resultLocal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetReportedMessagesAsync for classId {classId}, userId {userId}", chatId, currentUserId);
                throw;
            }
        }



        /// <summary>
        /// ایجاد کوئری پایه برای دریافت پیام‌های گروه
        /// همچنین باید پیامهایی که برای همه چتها ارسال شده اند را هم شامل شود
        /// </summary>
        /// <param name="chatId"></param>
        /// <param name="messageType">نوع چت</param>
        /// <returns></returns>
        private IQueryable<Message> GetMessagesQuery(long targetId, EnumMessageType messageType, long? currentUserId = null)
        {
            return messageType switch
            {
                EnumMessageType.Group => _context.Messages
                    .AsNoTracking()
                    .Where(m => m.OwnerId == targetId &&
                        !m.IsHidden && (m.MessageType == (byte)EnumMessageType.Group || m.MessageType == 3))
                    .Include(m => m.MessageTexts)
                    .Include(m => m.SenderUser)
                    .Include(m => m.ReplyMessage).ThenInclude(r => r.MessageTexts)
                    .Include(m => m.ReplyMessage).ThenInclude(r => r.SenderUser)
                    .Include(m => m.MessageFiles).ThenInclude(f => f.FileExtension)
                    .Include(m => m.MessageReads)
                    .Include(repo => repo.MessageFoulReports),

                EnumMessageType.Channel => _context.Messages
                    .AsNoTracking()
                    .Where(m => m.OwnerId == targetId &&
                        !m.IsHidden && (m.MessageType == (byte)EnumMessageType.Channel || m.MessageType == 4))
                    .Include(m => m.MessageTexts)
                    .Include(m => m.SenderUser)
                    .Include(m => m.ReplyMessage).ThenInclude(r => r.MessageTexts)
                    .Include(m => m.ReplyMessage).ThenInclude(r => r.SenderUser)
                    .Include(m => m.MessageFiles).ThenInclude(f => f.FileExtension)
                    .Include(m => m.MessageReads)
                    .Include(repo => repo.MessageFoulReports),

                EnumMessageType.Private => _context.Messages
                    .AsNoTracking()
                    .Where(m => m.MessageType == (byte)EnumMessageType.Private && 
                        !m.IsHidden && 
                        !m.IsSystemMessage &&
                        currentUserId.HasValue &&
                        ((m.SenderUserId == currentUserId.Value && m.OwnerId == targetId) ||
                         (m.SenderUserId == targetId && m.OwnerId == currentUserId.Value)))
                    .Include(m => m.MessageTexts)
                    .Include(m => m.SenderUser)
                    .Include(m => m.ReplyMessage).ThenInclude(r => r.MessageTexts)
                    .Include(m => m.ReplyMessage).ThenInclude(r => r.SenderUser)
                    .Include(m => m.MessageFiles).ThenInclude(f => f.FileExtension)
                    .Include(m => m.MessageReads)
                    .Include(repo => repo.MessageFoulReports),

                _ => throw new ArgumentException($"Unsupported message type: {messageType}")
            };
        }


        /// <summary>
        /// دریافت تمامی پیامهای گزارش شده در تمامی چت‌ها
        /// </summary>
        /// <param name="chatType"></param>
        /// <returns></returns>
        private IQueryable<Message> GetGlobalMessagesQuery(string chatType)
        {
            byte messageType = (byte)EnumMessageType.Group;
            byte messageTypeAll = 3;

            if (chatType == ConstChat.ChannelGroupType)
            {
                messageType = (byte)EnumMessageType.Channel;
                messageTypeAll = 4;
            }
            else if (chatType == ConstChat.PrivateType)
            {
                messageType = (byte)EnumMessageType.Private;
                messageTypeAll = 2; // For Private, use the same type since there's no "all private" category
            }

            var query = _context.Messages
                .AsNoTracking()
                .Where(m => !m.IsHidden && (m.MessageType == messageType || m.MessageType == messageTypeAll))
                .Include(m => m.MessageTexts)
                .Include(m => m.SenderUser)
                .Include(m => m.ReplyMessage).ThenInclude(r => r.MessageTexts)
                .Include(m => m.ReplyMessage).ThenInclude(r => r.SenderUser)
                .Include(m => m.MessageFiles).ThenInclude(f => f.FileExtension)
                .Include(m => m.MessageReads)
                .Include(repo => repo.MessageFoulReports);

            return query;

        }

        // متد کمکی برای تبدیل لیست پیام‌ها به DTO
        private List<MessageDto> MapMessagesToDto(List<Message> messages, long currentUserId)
        {
            if (messages == null || !messages.Any())
            {
                return new List<MessageDto>();
            }

            return messages.Select(m => new MessageDto
            {
                // ClassGroupId = m.ClassGroupMessages !=null ? m.ClassGroupMessages.FirstOrDefault().ClassId : m.ChannelMessages.FirstOrDefault().ChannelId,
                MessageId = m.MessageId,
                SenderUserId = m.SenderUserId,
                MessageDateTime = m.MessageDateTime,
                MessageType = m.MessageType,
                IsHidden = m.IsHidden,
                IsPin = m.IsPin,
                PinnedByUserId = m.PinnedByUserId,
                PinnedAt = m.PinnedAt,
                IsSystemMessage = m.IsSystemMessage,
                MessageFiles = m.MessageFiles?.Select(mf => new MessageFileDto
                {
                    MessageFileId = mf.MessageFileId,
                    OriginalFileName = mf.OriginalFileName,
                    FileName = mf.FileName,
                    FileSize = mf.FileSize,
                    CreateDate = mf.CreateDate,
                    FilePath = mf.FilePath,
                    FileThumbPath = mf.FileThumbPath,
                    FileExtension = new FileExtensionDto
                    {
                        Comment = mf.FileExtension.Comment,
                        Extension = mf.FileExtension.Extension,
                        FileExtensionId = mf.FileExtension.FileExtensionId,
                        FontAwesome = mf.FileExtension.FontAwesome,
                        Type = mf.FileExtension.Type
                    }
                }).ToList(),
                MessageText = m.MessageTexts?.Select(mt => new MessageTextDto
                {
                    MessageTextId = mt.MessageTextId,
                    MessageTxt = mt.MessageTxt
                }).FirstOrDefault(),
                ReplyMessageId = m.ReplyMessage?.MessageId,
                ReplyMessage = m.ReplyMessage == null ? null : new MessageDto
                {
                    MessageId = m.ReplyMessage.MessageId,
                    SenderUserId = m.ReplyMessage.SenderUserId,
                    MessageDateTime = m.ReplyMessage.MessageDateTime,
                    MessageType = m.ReplyMessage.MessageType,
                    IsHidden = m.ReplyMessage.IsHidden,
                    IsPin = m.ReplyMessage.IsPin,
                    MessageFiles = m.ReplyMessage.MessageFiles?.Select(mf => new MessageFileDto
                    {
                        MessageFileId = mf.MessageFileId,
                        OriginalFileName = mf.OriginalFileName,
                        FileName = mf.FileName,
                        FileSize = mf.FileSize,
                        CreateDate = mf.CreateDate,
                        FilePath = mf.FilePath,
                        FileThumbPath = mf.FileThumbPath,
                        FileExtension = new FileExtensionDto
                        {
                            Comment = mf.FileExtension.Comment,
                            Extension = mf.FileExtension.Extension,
                            FileExtensionId = mf.FileExtension.FileExtensionId,
                            FontAwesome = mf.FileExtension.FontAwesome,
                            Type = mf.FileExtension.Type
                        }
                    }).ToList(),
                    MessageText = m.ReplyMessage.MessageTexts?.Select(mt => new MessageTextDto
                    {
                        MessageTextId = mt.MessageTextId,
                        MessageTxt = mt.MessageTxt
                    }).FirstOrDefault(),
                    SenderUser = new UserDto
                    {
                        UserId = m.ReplyMessage.SenderUser.UserId,
                        NameFamily = m.ReplyMessage.SenderUser.NameFamily,
                        ProfilePicName = m.ReplyMessage.SenderUser.ProfilePicName
                    }
                },
                SenderUser = new UserDto
                {
                    UserId = m.SenderUserId,
                    DeptName = m.SenderUser.DeptName,
                    NameFamily = m.SenderUser.NameFamily,
                    ProfilePicName = m.SenderUser.ProfilePicName,
                    RoleFaName = m.SenderUser.RoleFaName,
                    RoleName = m.SenderUser.RoleName,
                },
                //IsReadByCurrentUser = readStatuses.Contains(m.MessageId) // Check if current message's ID is in the set of read messages
                IsReadByCurrentUser = m.MessageReads.Any(r => r.UserId == currentUserId && r.MessageId == m.MessageId),
                IsReadByAnyRecipient = (m.SenderUserId == currentUserId) && (m.MessageReads != null && m.MessageReads.Any(mr => mr.UserId != currentUserId)),
                MessageSeenCount = m.MessageReads?.Where(r => r.MessageId == m.MessageId && r.UserId != currentUserId).Count() ?? 0,
                IsEdited = m.IsEdited
            }).ToList();
        }


        // متد کمکی برای تبدیل لیست پیام‌ها خلاصه تر برای دریافت پیامهای گزارش شده
        private List<MessageDto> MapMessagesForReportedMessages(List<Message> messages, long currentUserId)
        {
            if (messages == null || !messages.Any())
            {
                return new List<MessageDto>();
            }

            return messages.Select(m => new MessageDto
            {
                OwnerId = m.OwnerId,
                MessageId = m.MessageId,
                SenderUserId = m.SenderUserId,
                MessageDateTime = m.MessageDateTime,
                MessageType = m.MessageType,
                IsHidden = m.IsHidden,
                IsPin = m.IsPin,
                PinnedByUserId = m.PinnedByUserId,
                PinnedAt = m.PinnedAt,
                IsSystemMessage = m.IsSystemMessage,
                MessageFiles = m.MessageFiles?.Select(mf => new MessageFileDto
                {
                    MessageFileId = mf.MessageFileId,
                    OriginalFileName = mf.OriginalFileName,
                    FileName = mf.FileName,
                    FileSize = mf.FileSize,
                    CreateDate = mf.CreateDate,
                    FilePath = mf.FilePath,
                    FileThumbPath = mf.FileThumbPath,
                    FileExtension = new FileExtensionDto
                    {
                        Comment = mf.FileExtension.Comment,
                        Extension = mf.FileExtension.Extension,
                        FileExtensionId = mf.FileExtension.FileExtensionId,
                        FontAwesome = mf.FileExtension.FontAwesome,
                        Type = mf.FileExtension.Type
                    }
                }).ToList(),
                MessageText = m.MessageTexts?.Select(mt => new MessageTextDto
                {
                    MessageTextId = mt.MessageTextId,
                    MessageTxt = mt.MessageTxt
                }).FirstOrDefault(),
                ReplyMessageId = m.ReplyMessage?.MessageId,

                SenderUser = new UserDto
                {
                    UserId = m.SenderUserId,
                    DeptName = m.SenderUser.DeptName,
                    NameFamily = m.SenderUser.NameFamily,
                    ProfilePicName = m.SenderUser.ProfilePicName,
                    RoleFaName = m.SenderUser.RoleFaName,
                    RoleName = m.SenderUser.RoleName,
                },
                //IsReadByCurrentUser = readStatuses.Contains(m.MessageId) // Check if current message's ID is in the set of read messages
                IsReadByCurrentUser = m.MessageReads.Any(r => r.UserId == currentUserId && r.MessageId == m.MessageId),
                IsReadByAnyRecipient = (m.SenderUserId == currentUserId) && (m.MessageReads != null && m.MessageReads.Any(mr => mr.UserId != currentUserId)),
                MessageSeenCount = m.MessageReads?.Where(r => r.MessageId == m.MessageId && r.UserId != currentUserId).Count() ?? 0,
                IsEdited = m.IsEdited
            }).ToList();
        }


        public async Task<IEnumerable<MessageDto>> GetClassOldGroupMessagesAsync(long classId, long currentUserId, int pageNumber, int pageSize, long messageId = 0)
        {
            try
            {
                _logger.LogInformation($"Getting messages for class group {classId} for user {currentUserId}");

                List<Message> messages;
                if (messageId > 0)
                {
                    messages = _context.Messages
                    .Include(m => m.MessageTexts)
                    .Include(m => m.SenderUser)
                    .Include(m => m.ReplyMessage)
                            .ThenInclude(rm => rm.MessageTexts)
                    .Include(m => m.ReplyMessage)
                            .ThenInclude(rm => rm.SenderUser)
                    .Include(m => m.MessageFiles).ThenInclude(mf => mf.FileExtension)
                    .Include(m => m.MessageReads)
                    .Where(m => m.OwnerId == classId && m.IsHidden == false && m.MessageId < messageId)
                    .OrderByDescending(m => m.MessageDateTime)
                    .Take(pageSize)
                    .ToList();
                }
                else
                {
                    messages = await _context.Messages
                   .Include(m => m.MessageTexts)
                   .Include(m => m.SenderUser)
                   .Include(m => m.ReplyMessage)
                           .ThenInclude(rm => rm.MessageTexts)
                   .Include(m => m.ReplyMessage)
                           .ThenInclude(rm => rm.SenderUser)
                   .Include(m => m.MessageFiles).ThenInclude(mf => mf.FileExtension)
                   .Include(m => m.MessageReads) // Added include for MessageReads
                   .Where(m => m.OwnerId == classId && m.IsHidden == false)
                   .OrderByDescending(m => m.MessageDateTime)
                   .Skip((pageNumber - 1) * pageSize)
                   .Take(pageSize)
                   .ToListAsync();
                }

                // Map to DTOs
                return messages.Select(m => new MessageDto
                {
                    MessageId = m.MessageId,
                    SenderUserId = m.SenderUserId,
                    MessageDateTime = m.MessageDateTime,
                    MessageType = m.MessageType,
                    IsHidden = m.IsHidden,
                    IsPin = m.IsPin,
                    MessageFiles = m.MessageFiles?.Select(mf => new MessageFileDto
                    {
                        MessageFileId = mf.MessageFileId,
                        OriginalFileName = mf.OriginalFileName,
                        FileName = mf.FileName,
                        FileSize = mf.FileSize,
                        CreateDate = mf.CreateDate,
                        FilePath = mf.FilePath,
                        FileThumbPath = mf.FileThumbPath,
                        FileExtension = new FileExtensionDto
                        {
                            Comment = mf.FileExtension.Comment,
                            Extension = mf.FileExtension.Extension,
                            FileExtensionId = mf.FileExtension.FileExtensionId,
                            FontAwesome = mf.FileExtension.FontAwesome,
                            Type = mf.FileExtension.Type
                        }
                    }).ToList(),
                    MessageText = m.MessageTexts?.Select(mt => new MessageTextDto
                    {
                        MessageTextId = mt.MessageTextId,
                        MessageTxt = mt.MessageTxt
                    }).FirstOrDefault(),
                    ReplyMessageId = m.ReplyMessage?.MessageId,
                    ReplyMessage = m.ReplyMessage == null ? null : new MessageDto
                    {
                        MessageId = m.ReplyMessage.MessageId,
                        SenderUserId = m.ReplyMessage.SenderUserId,
                        MessageDateTime = m.ReplyMessage.MessageDateTime,
                        MessageType = m.ReplyMessage.MessageType,
                        IsHidden = m.ReplyMessage.IsHidden,
                        IsPin = m.ReplyMessage.IsPin,
                        MessageFiles = m.ReplyMessage.MessageFiles?.Select(mf => new MessageFileDto
                        {
                            MessageFileId = mf.MessageFileId,
                            OriginalFileName = mf.OriginalFileName,
                            FileName = mf.FileName,
                            FileSize = mf.FileSize,
                            CreateDate = mf.CreateDate,
                            FilePath = mf.FilePath,
                            FileThumbPath = mf.FileThumbPath,
                            FileExtension = new FileExtensionDto
                            {
                                Comment = mf.FileExtension.Comment,
                                Extension = mf.FileExtension.Extension,
                                FileExtensionId = mf.FileExtension.FileExtensionId,
                                FontAwesome = mf.FileExtension.FontAwesome,
                                Type = mf.FileExtension.Type
                            }
                        }).ToList(),
                        MessageText = m.ReplyMessage.MessageTexts?.Select(mt => new MessageTextDto
                        {
                            MessageTextId = mt.MessageTextId,
                            MessageTxt = mt.MessageTxt
                        }).FirstOrDefault(),
                        SenderUser = new UserDto
                        {
                            UserId = m.ReplyMessage.SenderUser.UserId,
                            NameFamily = m.ReplyMessage.SenderUser.NameFamily,
                            ProfilePicName = m.ReplyMessage.SenderUser.ProfilePicName
                        }
                    },
                    SenderUser = new UserDto
                    {
                        UserId = m.SenderUserId,
                        DeptName = m.SenderUser.DeptName,
                        NameFamily = m.SenderUser.NameFamily,
                        ProfilePicName = m.SenderUser.ProfilePicName,
                        RoleFaName = m.SenderUser.RoleFaName,
                        RoleName = m.SenderUser.RoleName,
                    },
                    //IsReadByCurrentUser = readStatuses.Contains(m.MessageId) // Check if current message's ID is in the set of read messages
                    IsReadByCurrentUser = m.MessageReads.Any(r => r.UserId == currentUserId && r.MessageId == m.MessageId),
                    IsReadByAnyRecipient = (m.SenderUserId == currentUserId) && (m.MessageReads != null && m.MessageReads.Any(mr => mr.UserId != currentUserId)),
                    MessageSeenCount = m.MessageReads?.Where(r => r.MessageId == m.MessageId && r.UserId != currentUserId).Count() ?? 0,

                }).ToList();
            }
            catch (Exception ex)
            {
                //return 
                throw;
            }
        }

        public async Task<long?> MarkMessageAsReadAsync(long messageId, long userId, long targetId, string groupTpe)
        {
            _logger.LogInformation($"Attempting to mark message {messageId} as read for user {userId}");

            // First, find the message to get its SenderUserId
            var message = await _context.Messages
                                        .AsNoTracking() // Use AsNoTracking if you only need to read data
                                        .FirstOrDefaultAsync(m => m.MessageId == messageId);

            if (message == null)
            {
                _logger.LogWarning($"Message {messageId} not found. Cannot mark as read or retrieve sender.");
                return null; // Message not found, so sender ID is unknown
            }

            // Check if already marked as read
            var existingReadStatus = await _context.MessageReads
                .FirstOrDefaultAsync(mr => mr.MessageId == messageId && mr.UserId == userId && mr.TargetId == targetId);

            if (existingReadStatus != null)
            {
                _logger.LogInformation($"Message {messageId} already marked as read for user {userId}. Returning sender ID: {message.SenderUserId}.");
                return message.SenderUserId; // Already marked, just return the sender ID
            }

            // Create MessageRead entity
            var messageReadEntity = new MessageRead
            {
                MessageId = messageId,
                UserId = userId,
                ReadDateTime = DateTime.UtcNow, // Consistent with other DateTime additions
                TargetId = targetId,
                GroupType = groupTpe
            };
            _context.MessageReads.Add(messageReadEntity); // Changed from _context.Add for clarity

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Message {messageId} successfully marked as read for user {userId}. Returning sender ID: {message.SenderUserId}.");
                return message.SenderUserId; // Return sender ID after successful save
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving read status for message {messageId}, user {userId}.");
                return null; // Error occurred during save
            }
        }

        public async Task<IEnumerable<MessageReadDto>> GetMessageReadStatusAsync(long messageId)
        {
            Console.WriteLine($"Getting read status for message {messageId}");
            // Query MessageRead entities for the given messageId
            var messageReadEntities = await _context.MessageReads
                .Include(mr => mr.User)
                .Where(mr => mr.MessageId == messageId)
                .ToListAsync();
            // Map to DTOs

            return new List<MessageReadDto>(messageReadEntities.Select(mr => new MessageReadDto
            {
                MessageId = mr.MessageId,
                UserId = mr.UserId,
                ReadDateTime = mr.ReadDateTime,
                //UserName = mr.User.UserName // Assuming User entity has a UserName property
            }));
        }

        public async Task PinMessageAsync(long userId, long messageId, bool isPinned)
        {
            Console.WriteLine($"Setting pin status for message {messageId} to {isPinned}");

            // Load message with related group/channel data needed for authorization checks
            var messageEntity = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == messageId);

            if (messageEntity == null) throw new Exception("Message not found");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not found or not authorized to pin/unpin this message.");

            // Managers can always pin/unpin
            if (user.RoleName == ConstRoles.Manager)
            {
                messageEntity.IsPin = isPinned;
                messageEntity.PinnedAt = DateTime.UtcNow;
                messageEntity.PinnedByUserId = userId;
                _context.Messages.Update(messageEntity);
                await _context.SaveChangesAsync();
                return;
            }

            // Only Manager, Personel (with access) or Teacher (for class groups where they are the teacher) can pin
            if (messageEntity.MessageType == (byte)EnumMessageType.Group)
            {
                var classId = messageEntity.OwnerId;
                if (classId == 0)
                    throw new UnauthorizedAccessException("Unable to determine group for this message.");

                if (user.RoleName == ConstRoles.Teacher)
                {
                    // verify teacher is assigned to this class
                    var isTeacherInGroup = _context.ClassGroups.Any(f => f.TeacherUserId == user.UserId);

                    if (!isTeacherInGroup)
                        throw new UnauthorizedAccessException("User is not authorized as a teacher in this group chat.");
                }
                else if (user.RoleName == ConstRoles.Personel)
                {
                    // personnel must have explicit access in personelchataccess table for this class group
                    var hasAccess = await _personnelChatAccessService.HasAccessToSendToChatAsync(userId, classId, ConstChat.ClassGroupType);
                    if (!hasAccess)
                        throw new UnauthorizedAccessException("Personnel does not have access to this class group.");
                }
                else
                {
                    throw new UnauthorizedAccessException("User role not authorized to pin/unpin messages in group chat.");
                }
            }
            else if (messageEntity.MessageType == (byte)EnumMessageType.Channel)
            {
                var channelId = messageEntity.OwnerId; ;
                if (channelId == 0)
                    throw new UnauthorizedAccessException("Unable to determine channel for this message.");

                if (user.RoleName == ConstRoles.Personel)
                {
                    // personnel must have explicit access in personelchataccess table for this channel
                    var hasAccess = await _personnelChatAccessService.HasAccessToSendToChatAsync(userId, channelId, ConstChat.ChannelGroupType);
                    if (!hasAccess)
                        throw new UnauthorizedAccessException("Personnel does not have access to this channel.");
                }
                else
                {
                    // Teachers (unless also managers) are not allowed to pin channel messages by policy
                    throw new UnauthorizedAccessException("User role not authorized to pin/unpin messages in channel chat.");
                }
            }
            else
            {
                throw new UnauthorizedAccessException("Unsupported message chat type for pin/unpin operation.");
            }

            // Update pin state
            messageEntity.IsPin = isPinned;
            messageEntity.PinnedAt = DateTime.UtcNow;
            messageEntity.PinnedByUserId = userId;

            _context.Messages.Update(messageEntity);
            await _context.SaveChangesAsync();


        }

        // Helper to notify group and bridges about pin/unpin


        /// <summary>
        /// isHiding = Delete message
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<ActionMessageDto?> HideMessageAsync(long messageId, long userId, long groupId, string groupType, bool isPortalMessage)
        {
            _logger.LogInformation($"Hiding message {messageId} for user {userId}");

            // Include related data needed for DTO conversion and group identification

            byte messageType = GetMessageType(groupId, groupType);

            var messageEntity = await _context.Messages
                .Include(m => m.MessageTexts)
                .Include(m => m.SenderUser)
                .Include(m => m.MessageFiles)
                    .ThenInclude(mf => mf.FileExtension)
                .Include(m => m.ReplyMessage)
                    .ThenInclude(rm => rm.MessageTexts)
                .Include(m => m.ReplyMessage)
                    .ThenInclude(rm => rm.SenderUser)
                .FirstOrDefaultAsync(m => m.MessageId == messageId && m.MessageType == messageType);

            if (messageEntity == null)
            {
                _logger.LogWarning($"HideMessageAsync: Message {messageId} not found.");
                return null;
            }

            //has Access    
            var hasAccess = HasAccessToMessage(userId, messageEntity);

            var timeLimit = TimeSpan.FromMinutes(_timeSettings.TimeToDeleteMessagesInMinutes); // زمان مجاز برای حذف پیام

            //if (DateTime.UtcNow - messageEntity.MessageDateTime > timeLimit)
            //    throw new Exception($"Delete time limit exceeded. You can only delete messages within {timeLimit} minutes.");

            if (DateTime.UtcNow - messageEntity.MessageDateTime > timeLimit)
                throw new TimeLimitExceededException(_timeSettings.TimeToDeleteMessagesInMinutes);


            messageEntity.IsHidden = true;
            _context.Messages.Update(messageEntity);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Hide message {messageId} by user {userId} succeeded.");

            var chatType = messageEntity.MessageType;
            long classGroupID = messageEntity.OwnerId;

            // Map to DTO - reusing GetMessageByIdAsync logic structure but on existing entity

            var signalRGroupKey = GenerateSignalRGroupKey.GenerateKey(classGroupID, "");

            return new ActionMessageDto
            {
                MessageId = messageEntity.MessageId,
                UserId = messageEntity.SenderUserId,
                MessageType = chatType,
                ClassGroupId = classGroupID,
                GroupType = groupType
            };

        }

        //TODO برقرار بشه
        public async Task<MessageFoulReportDto> ReportMessageAsync(long messageId, long reporterUserId, string reason)
        {
            Console.WriteLine($"Reporting message {messageId} by user {reporterUserId}");
            // Create MessageFoulReport entity
            var messageFoulReportEntity = new MessageFoulReport
            {
                MessageId = messageId,
                FoulReporterUserId = reporterUserId,
                FoulReportDateTime = DateTime.UtcNow,
                FoulDesc = reason
            };
            _context.Add(messageFoulReportEntity);
            // Save changes
            await _context.SaveChangesAsync();
            // Map to DTO

            return new MessageFoulReportDto
            {
                MessageId = messageId,
                // FoulReporterUserId = reporterUserId,
                // FoulReportDateTime = messageFoulReportEntity.FoulReportDateTime,
                FoulDesc = reason
            };
        }

        public async Task SaveMessageAsync(long messageId, long userId)
        {
            Console.WriteLine($"Saving message {messageId} for user {userId}");
            // Create MessageSaved entity
            var messageSavedEntity = new MessageSaved
            {
                MessageId = messageId,
                UserId = userId,
                SaveDateTime = DateTime.UtcNow
            };
            _context.Add(messageSavedEntity);
            // Save changes
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// پیامهای ذخیره شده افراد
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<MessageSavedDto>> GetSavedMessagesAsync(long userId)
        {
            try
            {
                var messages = await _context.MessageSaveds
                    .Include(m => m.Message)
                    .Include(m => m.Message.MessageTexts)
                    .Include(m => m.Message.SenderUser)
                    .Include(m => m.Message.ReplyMessage)
                            .ThenInclude(rm => rm.MessageTexts)
                    .Include(m => m.Message.ReplyMessage)
                            .ThenInclude(rm => rm.SenderUser)
                    .Include(m => m.Message.MessageFiles).ThenInclude(mf => mf.FileExtension)
                    .Include(m => m.Message.MessageReads)
                    .Where(m => !m.Message.IsHidden && m.UserId == userId)
                    .OrderByDescending(m => m.SaveDateTime)
                    .ToListAsync();


                // Map to DTOs

                return new List<MessageSavedDto>(messages.Select(ms => new MessageSavedDto
                {
                    MessageSavedId = ms.MessageSavedId,
                    MessageId = ms.MessageId,
                    UserId = ms.UserId,
                    SaveDateTime = ms.SaveDateTime,
                    Message = new MessageDto
                    {
                        MessageId = ms.MessageId,
                        SenderUserId = ms.Message.SenderUserId,
                        MessageDateTime = ms.Message.MessageDateTime,
                        MessageType = ms.Message.MessageType,
                        IsHidden = ms.Message.IsHidden,
                        IsPin = ms.Message.IsPin,
                        MessageFiles = ms.Message.MessageFiles?.Select(mf => new MessageFileDto
                        {
                            MessageFileId = mf.MessageFileId,
                            OriginalFileName = mf.OriginalFileName,
                            FileName = mf.FileName,
                            FileSize = mf.FileSize,
                            CreateDate = mf.CreateDate,
                            FilePath = mf.FilePath,
                            FileThumbPath = mf.FileThumbPath,
                            FileExtension = new FileExtensionDto
                            {
                                Comment = mf.FileExtension.Comment,
                                Extension = mf.FileExtension.Extension,
                                FileExtensionId = mf.FileExtension.FileExtensionId,
                                FontAwesome = mf.FileExtension.FontAwesome,
                                Type = mf.FileExtension.Type
                            }
                        }).ToList(),
                        MessageText = ms.Message.MessageTexts?.Select(mt => new MessageTextDto
                        {
                            MessageTextId = mt.MessageTextId,
                            MessageTxt = mt.MessageTxt
                        }).FirstOrDefault(),
                        ReplyMessageId = ms.Message.ReplyMessage?.MessageId,
                        ReplyMessage = ms.Message.ReplyMessage == null ? null : new MessageDto
                        {
                            MessageId = ms.Message.ReplyMessage.MessageId,
                            SenderUserId = ms.Message.ReplyMessage.SenderUserId,
                            MessageDateTime = ms.Message.ReplyMessage.MessageDateTime,
                            MessageType = ms.Message.ReplyMessage.MessageType,
                            IsHidden = ms.Message.ReplyMessage.IsHidden,
                            IsPin = ms.Message.ReplyMessage.IsPin,
                            MessageFiles = ms.Message.ReplyMessage.MessageFiles?.Select(mf => new MessageFileDto
                            {
                                MessageFileId = mf.MessageFileId,
                                OriginalFileName = mf.OriginalFileName,
                                FileName = mf.FileName,
                                FileSize = mf.FileSize,
                                CreateDate = mf.CreateDate,
                                FilePath = mf.FilePath,
                                FileThumbPath = mf.FileThumbPath,
                                FileExtension = new FileExtensionDto
                                {
                                    Comment = mf.FileExtension.Comment,
                                    Extension = mf.FileExtension.Extension,
                                    FileExtensionId = mf.FileExtension.FileExtensionId,
                                    FontAwesome = mf.FileExtension.FontAwesome,
                                    Type = mf.FileExtension.Type
                                }
                            }).ToList(),
                            MessageText = ms.Message.ReplyMessage.MessageTexts?.Select(mt => new MessageTextDto
                            {
                                MessageTextId = mt.MessageTextId,
                                MessageTxt = mt.MessageTxt
                            }).FirstOrDefault(),
                            SenderUser = new UserDto
                            {
                                UserId = ms.Message.ReplyMessage.SenderUser.UserId,
                                NameFamily = ms.Message.ReplyMessage.SenderUser.NameFamily,
                                ProfilePicName = ms.Message.ReplyMessage.SenderUser.ProfilePicName
                            }
                        },
                        SenderUser = new UserDto
                        {
                            UserId = ms.Message.SenderUserId,
                            DeptName = ms.Message.SenderUser.DeptName,
                            NameFamily = ms.Message.SenderUser.NameFamily,
                            ProfilePicName = ms.Message.SenderUser.ProfilePicName,
                            RoleFaName = ms.Message.SenderUser.RoleFaName,
                            RoleName = ms.Message.SenderUser.RoleName,
                        },
                        //IsReadByCurrentUser = readStatuses.Contains(m.MessageId) // Check if current message's ID is in the set of read messages
                        IsReadByCurrentUser = ms.Message.MessageReads.Any(r => r.UserId == userId && r.MessageId == ms.MessageId),
                        IsReadByAnyRecipient = (ms.Message.SenderUserId == userId) && (ms.Message.MessageReads != null && ms.Message.MessageReads.Any(mr => mr.UserId != userId)),
                        MessageSeenCount = ms.Message.MessageReads?.Where(r => r.MessageId == ms.MessageId && r.UserId != userId).Count() ?? 0,

                    }
                }));
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task DeleteSavedMessageAsync(long messageSavedId, long userId)
        {
            _logger.LogInformation($"Deleting saved message {messageSavedId} for user {userId}");
            // Find MessageSaved entity by ID
            var messageSavedEntity = await _context.MessageSaveds.FindAsync(messageSavedId);
            if (messageSavedEntity == null) throw new Exception("Saved message not found");
            // Verify userId matches the owner
            if (messageSavedEntity.UserId != userId) throw new Exception("User not authorized to delete this saved message");
            // Remove entity
            _context.Remove(messageSavedEntity);
            // Save changes
            await _context.SaveChangesAsync();
        }


        private byte GetMessageType(long chatId, string chatType)
        {
            byte messageTypeByte = 0;
            if (chatType == ConstChat.ClassGroupType)
            {
                messageTypeByte = (byte)EnumMessageType.Group;
            }
            else if (chatType == ConstChat.ChannelGroupType)
            {
                messageTypeByte = (byte)EnumMessageType.Channel;
            }
            else if (chatType == ConstChat.PrivateType)
            {
                messageTypeByte = (byte)EnumMessageType.Private;
            }
            else
            {
                throw new Exception("Chat Type Required!");
            }
            return messageTypeByte;
        }



        public async Task DeletePrivateMessageAsync(long senderUserId, long messageId)
        {
            _logger.LogInformation($"Deleting private message {messageId} for user {senderUserId}");
            // Find MessageSaved entity by ID
            var messageEntity = await _context.Messages
                 .Where(w => w.MessageId == messageId).FirstOrDefaultAsync();

            if (messageEntity == null) throw new Exception("Message not found");

            //has access
            var hasAccess = HasAccessToMessage(senderUserId, messageEntity);


            // Verify userId matches the owner
            if (messageEntity.SenderUserId != senderUserId) throw new Exception("User not authorized to delete this message");
            // Soft delete entity = isHidden = true
            messageEntity.IsHidden = true;
            _context.Messages.Update(messageEntity);
            // Save changes
            await _context.SaveChangesAsync();
        }

        // --- Edit Messages ---

        /// <summary>
        /// ویرایش یک پیام در گروه
        /// </summary>
        /// <param name="messageId">ایدی پیام</param>
        /// <param name="editorUserId">ایدی کاربر ویرایش کننده</param>
        /// <param name="newMessageText">متن ویرایش شده در صورت موجود</param>
        /// <param name="fileIds">فایلهای جدیدی که ممکن است اضافه شده باشند</param>
        /// <param name="fileIdsToRemove">فایلهایی که باید حذف شوند</param>
        /// <returns></returns>
        public async Task<MessageDto> EditMessageAsync(long messageId, long editorUserId, long targetId, string groupType,
             string? newMessageText = null, List<long>? fileIds = null, List<long>? fileIdsToRemove = null)
        {
            _logger.LogInformation($"Attempting to edit class group message {messageId} by user {editorUserId}");

            var filesToPermanentlyDelete = new List<string>();
            // شروع تراکنش
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                //اگر چت کانال بود فقط مدیر کانال و مالک کانال میتونه پیام ارسال کنه
                if (groupType == ConstChat.ChannelGroupType)
                {
                    var channelMember = await _context.ChannelMembers.FirstOrDefaultAsync(f => f.UserId == editorUserId);
                    if (channelMember == null)
                        throw new Exception("User not found!");

                    if (channelMember.MemberRoleType != (byte)EnumMemberRoleType.admin && channelMember.MemberRoleType != (byte)EnumMemberRoleType.owner)
                        throw new Exception("User access denied!");
                }


                // بررسی وجود پیام و دسترسی کاربر
                var messageEntity = await _context.Messages
                    .FirstOrDefaultAsync(m => m.MessageId == messageId && m.SenderUserId == editorUserId && m.OwnerId == targetId);

                if (messageEntity == null)
                    throw new Exception("Message not found");



                // بررسی اینکه کاربر فرستنده پیام است
                if (messageEntity.SenderUserId != editorUserId)
                    throw new Exception("You can only edit your own messages");

                // بررسی عضویت در گروه کلاسی
                var isMember = await _classGroupService.IsUserMemberOfClassGroupAsync(editorUserId, targetId);
                if (!isMember)
                    throw new Exception("User is not a member of this class group");

                // بررسی محدودیت زمانی برای ویرایش 
                var timeLimit = TimeSpan.FromMinutes(_timeSettings.TimeToEditMessagesInMinutes);

                if (DateTime.UtcNow - messageEntity.MessageDateTime > timeLimit)
                    throw new TimeLimitExceededException(_timeSettings.TimeToEditMessagesInMinutes);

                // ویرایش متن پیام
                if (newMessageText != null)
                {
                    var existingMessageText = await _context.MessageTexts
                        .FirstOrDefaultAsync(mt => mt.MessageId == messageId);

                    if (string.IsNullOrEmpty(newMessageText))
                    {
                        // حذف متن در صورت خالی بودن
                        if (existingMessageText != null)
                        {
                            _context.MessageTexts.Remove(existingMessageText);
                        }
                    }
                    else
                    {
                        if (existingMessageText != null)
                        {
                            // ویرایش متن موجود
                            existingMessageText.MessageTxt = newMessageText;
                            _context.MessageTexts.Update(existingMessageText);
                        }
                        else
                        {
                            // افزودن متن جدید
                            var newMessageTextEntity = new MessageText
                            {
                                MessageTxt = newMessageText,
                                MessageId = messageId
                            };
                            _context.MessageTexts.Add(newMessageTextEntity);
                        }
                    }
                }

                // حذف فایلهایی که باید حذف شوند
                if (fileIdsToRemove != null && fileIdsToRemove.Any())
                {
                    foreach (var fileId in fileIdsToRemove)
                    {
                        var messageFile = await _context.MessageFiles.FirstOrDefaultAsync(f => f.MessageFileId == fileId);

                        if (messageFile == null)
                            throw new Exception($"File with ID {fileId} not found or already attached.");

                        string originalPath = _fileManagementService.GetFullPath($"{editorUserId}/{messageFile.FileName}");
                        string deletePath = _fileManagementService.GetDeletePath($"{editorUserId}/{messageFile.FileName}");

                        if (!await _fileManagementService.MoveFileAsync(originalPath, deletePath))
                            throw new Exception("Error moving file to temporary delete folder");


                        filesToPermanentlyDelete.Add(deletePath);

                        _context.MessageFiles.Remove(messageFile);
                    }
                }


                // افزودن فایل‌های جدید
                var newMessageFiles = new List<MessageFile>();
                if (fileIds != null && fileIds.Any())
                {
                    // بررسی تعداد کل فایل‌ها بعد از ویرایش
                    var existingFilesCount = await _context.MessageFiles
                        .CountAsync(mf => mf.MessageId == messageId);

                    //  مجموع فایلهایی که میتونه یک پیام داشته باشه 10 عدد هست
                    var finalFileCount = existingFilesCount - fileIdsToRemove?.Count() + fileIds.Count;

                    if (finalFileCount > 10) // محدودیت تعداد فایل
                        throw new Exception("Maximum 10 files allowed per message");

                    //TODO : اضافه کردن ایدی فایلهای جدید دریافتی به پیام

                    foreach (var fileId in fileIds)
                    {
                        var messageFile = await _context.MessageFiles.FirstOrDefaultAsync(f => f.MessageFileId == fileId && f.MessageId == null);

                        if (messageFile == null)
                            throw new Exception($"File with ID {fileId} not found or already attached.");

                        // **اتصال فایل به پیام ارسالی**
                        messageFile.MessageId = messageEntity.MessageId;
                        _context.MessageFiles.Update(messageFile);
                    }

                }

                // به‌روزرسانی زمان آخرین ویرایش
                messageEntity.LastEditDateTime = DateTime.UtcNow;
                messageEntity.IsEdited = true;
                _context.Messages.Update(messageEntity);

                // ذخیره تمام تغییرات
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // بازگرداندن پیام به‌روزرسانی شده
                //TODO باید بصورت مدلی  MessageDto باشد
                var updatedMessage = await GetMessageByIdAsync(editorUserId, messageId);

                //ساخت مدل کامل جهت برگرداندن

                _logger.LogInformation($"Successfully edited class group message {messageId}");

                return updatedMessage;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error occurred while editing class group message {messageId}.");

                // برگرداندن فایل‌ها به محل اصلی اگر خطایی رخ داد
                foreach (var tempPath in filesToPermanentlyDelete)
                {
                    var originalPath = _fileManagementService.GetOriginalPathFromTemp(tempPath);
                    await _fileManagementService.MoveFileAsync(tempPath, originalPath);
                }

                throw;
            }
        }


        public Task<MessageDto> EditChannelMessageAsync(
            long messageId, long editorUserId, long targetId,
            string? newMessageText = null, List<long>? fileIds = null, List<long>? fileIdsToRemove = null)
        {
            throw new NotImplementedException();
        }


        public Task<MessageDto> EditPrivateMessageAsync(
            long senderUserId, long messageId, long receiverUserId,
            string messageText, List<IFormFile>? files = null, long? replyToMessageId = null)
        {
            throw new NotImplementedException();
        }


        #region ارتباط با redis

        public async Task<int> GetUnreadCountAsync(long userId, string groupType, long targetId)
        {
            if (groupType != ConstChat.ClassGroupType && groupType != ConstChat.ChannelGroupType && groupType != ConstChat.PrivateType)
                throw new ArgumentException("Invalid groupType. Must be 'ClassGroup', 'ChannelGroup', or 'Private'.");

            // Use the new Redis method to count unread messages
            return await _redisUnreadManage.GetUnreadCountAsync(userId, targetId, groupType);
        }


        /// <summary>
        /// دریافت تمام پیامهای خوانده نشده این کاربر چت
        /// </summary>
        /// <param name="targetId">ایدی چت - گروه / کانال</param>
        /// <param name="groupType"></param>
        /// <returns></returns>
        public async Task<List<UnreadMessageDto>> GetAllUnreadMessageInChat(long userId, long targetId, string groupType)
        {
            // ایدی اخرین پیام خوانده شده کاربر
            long lastReadMessageId = await _redisUnreadManage.GetLastReadMessageIdAsync(userId, targetId, groupType);

            byte messageTypeByte = GetMessageType(targetId, groupType);

            var allMessagesInChat =
                    await _context.Messages
                        .Where(m => m.OwnerId == targetId && m.MessageId > lastReadMessageId)
                        .Select(m => new UnreadMessageDto
                        {
                            MessageId = m.MessageId,
                            SenderUserId = m.SenderUserId
                        })
                        .ToListAsync();
            return allMessagesInChat;
        }



        /// <summary>
        ///  محاسبه تعداد پیام‌های خوانده نشده از SQL Server
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="targetId"></param>
        /// <param name="groupType"></param>
        /// <returns></returns>
        public async Task<int> CalculateUnreadCountFromSqlAsync(long userId, long targetId, string groupType)
        {
            try
            {
                // تمام MessageId های مربوط به این چت را از دیتابیس بگیرید
                IQueryable<long> chatMessageIdsQuery;

                byte messageTypeByte = (byte)EnumMessageType.Group;
                if (groupType == ConstChat.ChannelGroupType)
                {
                    messageTypeByte = (byte)EnumMessageType.Channel;
                }
                else if (groupType == ConstChat.PrivateType)
                {
                    messageTypeByte = (byte)EnumMessageType.Private;
                }

                // برای چت خصوصی، باید از OwnerId استفاده کنیم
                if (groupType == ConstChat.PrivateType)
                {
                    chatMessageIdsQuery = _context.Messages
                        .Where(m => m.MessageType == messageTypeByte && !m.IsSystemMessage &&
                                    ((m.SenderUserId == userId && m.OwnerId == targetId) ||
                                     (m.SenderUserId == targetId && m.OwnerId == userId)))
                        .Select(m => m.MessageId);
                }
                else
                {
                    chatMessageIdsQuery = _context.Messages
                        .Where(m => m.OwnerId == targetId && m.MessageType == messageTypeByte)
                        .Select(m => m.MessageId);
                }


                // ایدی اخرین پیام خوانده شده کاربر
                var lastReadMessageId = await GetLastReadMessageIdFromSqlAsync(userId, targetId, groupType);

                // تعداد پیام‌هایی که کاربر هنوز نخوانده است را بشمارید
                // این یعنی پیام‌هایی که در chatMessageIdsQuery هستند اما در MessageReads برای این کاربر نیستند.
                var unreadCount = await _context.Messages
                    .OrderBy(o => o.MessageId)
                    .Where(m => chatMessageIdsQuery.Contains(m.MessageId)
                    && m.MessageId > lastReadMessageId && !m.IsHidden && m.SenderUserId != userId
                    && !m.MessageReads.Any(mr => mr.UserId == userId))
                    .CountAsync();

                return unreadCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating unread count from SQL for user {UserId} in {GroupType} {TargetId}.", userId, groupType, targetId);
                return 0;
            }
        }


        /// <summary>
        /// Gets the DateTime of the last message read by a user in a specific chat.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="targetId">The ID of the group or channel.</param>
        /// <param name="groupType">The type of the group (ConstChat.ClassGroupType or ConstChat.ChannelGroupType).</param>
        /// <returns>The DateTime of the last read message, or null if no messages have been read.</returns>
        public async Task<DateTime?> GetLastReadMessageDateTimeAsync(long userId, long targetId, string groupType)
        {
            try
            {
                // ابتدا MessageId های مربوط به این چت را پیدا کنید
                IQueryable<long> chatMessageIdsQuery;
                if (groupType == ConstChat.ClassGroupType)
                {
                    chatMessageIdsQuery = _context.Messages
                        .Where(cgm => cgm.OwnerId == targetId)
                        .Select(cgm => cgm.MessageId);
                }
                else if (groupType == ConstChat.ChannelGroupType)
                {
                    chatMessageIdsQuery = _context.Messages
                        .Where(chm => chm.OwnerId == targetId)
                        .Select(chm => chm.MessageId);
                }
                else if (groupType == ConstChat.PrivateType)
                {
                    // برای چت خصوصی، باید از OwnerId استفاده کنیم
                    chatMessageIdsQuery = _context.Messages
                        .Where(m => m.MessageType == (byte)EnumMessageType.Private && !m.IsSystemMessage &&
                                    ((m.SenderUserId == userId && m.OwnerId == targetId) ||
                                     (m.SenderUserId == targetId && m.OwnerId == userId)))
                        .Select(m => m.MessageId);
                }
                else
                {
                    _logger.LogWarning("Invalid groupType '{GroupType}' provided for GetLastReadMessageDateTimeAsync.", groupType);
                    return null;
                }

                // سپس از MessageReads، آخرین زمان خوانده شدن پیام برای این کاربر و این چت را پیدا کنید
                var lastRead = await _context.MessageReads
                    .Where(mr => mr.UserId == userId && chatMessageIdsQuery.Contains(mr.MessageId))
                    .OrderByDescending(mr => mr.ReadDateTime)
                    .Select(mr => mr.ReadDateTime)
                    .FirstOrDefaultAsync();

                return lastRead;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last read message DateTime for user {UserId} in {GroupType} {TargetId}.", userId, groupType, targetId);
                return null;
            }
        }

        public async Task<long> GetLastReadMessageIdFromSqlAsync(long userId, long targetId, string groupType)
        {
            try
            {
                //یک ایندکس با مرتب سازی  DESC  ایجاد کردم روی ستونهای زیر 
                //--IX_MessageRead_User_Group_Target_Message
                // ON MessageReads (UserId, GroupType, TargetId, MessageId DESC);
                // که باعث میشه به سرعت دیتای مورد نظر هر کاربر رو بده

                var lastReadMessageId = await _context.MessageReads
                    .Where(mr => mr.UserId == userId &&
                                 mr.GroupType == groupType &&
                                 mr.TargetId == targetId)
                    .OrderByDescending(mr => mr.MessageId)
                    .Select(mr => (long?)mr.MessageId)
                    .FirstOrDefaultAsync();

                return lastReadMessageId ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error getting last read message ID for user {UserId} in {GroupType} {TargetId}.",
                    userId, groupType, targetId);
                return 0;
            }
        }



        #endregion


        #region Private Chats & System Messages

        public async Task<IEnumerable<PrivateChatItemDto>> GetUserPrivateChatsAsync(long userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"User {userId} not found");
                    return Enumerable.Empty<PrivateChatItemDto>();
                }

                var privateChats = new List<PrivateChatItemDto>();

                // 1. Get regular private messages (one-to-one)
                var regularPrivateMessages = await _context.Messages
                    .Where(m => m.MessageType == (byte)EnumMessageType.Private && !m.IsSystemMessage && !m.IsHidden)
                    .Where(m => m.SenderUserId == userId || m.OwnerId == userId)
                    .Include(m => m.MessageTexts)
                    .Include(m => m.SenderUser)
                    .OrderByDescending(m => m.MessageDateTime)
                    .ToListAsync();

                // Group by other user (using OwnerId/SenderUserId)
                var groupedByOtherUser = regularPrivateMessages
                    .GroupBy(m => m.SenderUserId == userId ? m.OwnerId : m.SenderUserId)
                    .Where(g => g.Key > 0)
                    .Select(g => new
                    {
                        OtherUserId = g.Key,
                        LastMessage = g.OrderByDescending(x => x.MessageDateTime).First()
                    })
                    .ToList();

                foreach (var chat in groupedByOtherUser)
                {
                    var otherUser = await _context.Users.FindAsync(chat.OtherUserId);
                    if (otherUser == null) continue;

                    // Count unread messages (messages sent by other user to current user)
                    var unreadCount = await _context.Messages
                        .Where(m => 
                            m.SenderUserId == chat.OtherUserId &&
                            m.OwnerId == userId &&
                            m.MessageType == (byte)EnumMessageType.Private &&
                            !m.MessageReads.Any(mr => mr.UserId == userId))
                        .CountAsync();

                    privateChats.Add(new PrivateChatItemDto
                    {
                        ChatId = chat.OtherUserId,
                        ChatKey = $"private_{chat.OtherUserId}",
                        ChatName = otherUser.NameFamily,
                        ProfilePicName = otherUser.ProfilePicName,
                        LastMessage = new ChatMessageDto
                        {
                            MessageId = chat.LastMessage.MessageId,
                            Text = chat.LastMessage.MessageTexts.FirstOrDefault()?.MessageTxt,
                            SenderId = chat.LastMessage.SenderUserId,
                            SenderName = chat.LastMessage.SenderUser?.NameFamily,
                            SentAt = chat.LastMessage.MessageDateTime
                        },
                        UnreadCount = unreadCount,
                        IsSystemChat = false,
                        OtherUserId = chat.OtherUserId,
                        LastMessageDateTime = chat.LastMessage.MessageDateTime
                    });
                }

                // 2. Get system messages based on user role
                var systemMessages = new List<Message>();

                // Check for messages sent to all students
                if (user.RoleName == ConstRoles.Student)
                {
                    var allStudentsMessages = await _context.Messages
                        .Where(m => m.IsSystemMessage && m.MessageType == (byte)EnumMessageType.AllStudents && !m.IsHidden)
                        .Include(m => m.MessageTexts)
                        .Include(m => m.SenderUser)
                        .OrderByDescending(m => m.MessageDateTime)
                        .ToListAsync();
                    systemMessages.AddRange(allStudentsMessages);
                }

                // Check for messages sent to all teachers
                if (user.RoleName == ConstRoles.Teacher)
                {
                    var allTeachersMessages = await _context.Messages
                        .Where(m => m.IsSystemMessage && m.MessageType == (byte)EnumMessageType.AllTeachers && !m.IsHidden)
                        .Include(m => m.MessageTexts)
                        .Include(m => m.SenderUser)
                        .OrderByDescending(m => m.MessageDateTime)
                        .ToListAsync();
                    systemMessages.AddRange(allTeachersMessages);
                }

                // Check for messages sent to all personnel
                if (user.RoleName == ConstRoles.Personel)
                {
                    var allPersonnelMessages = await _context.Messages
                        .Where(m => m.IsSystemMessage && m.MessageType == (byte)EnumMessageType.AllPersonel && !m.IsHidden)
                        .Include(m => m.MessageTexts)
                        .Include(m => m.SenderUser)
                        .OrderByDescending(m => m.MessageDateTime)
                        .ToListAsync();
                    systemMessages.AddRange(allPersonnelMessages);
                }

                // Check for messages sent to specific users via MessageRecipients
                var specificUserMessages = await _context.Messages
                    .Where(m => m.IsSystemMessage && m.MessageType == (byte)EnumMessageType.Private && !m.IsHidden)
                    .Where(m => m.MessageRecipients.Any(mr => mr.RecipientUserId == userId))
                    .Include(m => m.MessageTexts)
                    .Include(m => m.SenderUser)
                    .Include(m => m.MessageRecipients.Where(mr => mr.RecipientUserId == userId))
                    .OrderByDescending(m => m.MessageDateTime)
                    .ToListAsync();
                systemMessages.AddRange(specificUserMessages);

                // Add system chat if there are system messages
                if (systemMessages.Any())
                {
                    var lastSystemMessage = systemMessages.OrderByDescending(m => m.MessageDateTime).First();
                    
                    // Count unread system messages
                    var unreadSystemCount = systemMessages.Count(m => 
                        !m.MessageReads.Any(mr => mr.UserId == userId));

                    privateChats.Add(new PrivateChatItemDto
                    {
                        ChatId = userId, // Use current user's ID for system chat
                        ChatKey = $"systemchat_{userId}",
                        ChatName = "ایران اروپا", // School name from config
                        ProfilePicName = "system_icon",
                        LastMessage = new ChatMessageDto
                        {
                            MessageId = lastSystemMessage.MessageId,
                            Text = lastSystemMessage.MessageTexts.FirstOrDefault()?.MessageTxt,
                            SenderId = lastSystemMessage.SenderUserId,
                            SenderName = lastSystemMessage.SenderUser?.NameFamily,
                            SentAt = lastSystemMessage.MessageDateTime
                        },
                        UnreadCount = unreadSystemCount,
                        IsSystemChat = true,
                        OtherUserId = null,
                        LastMessageDateTime = lastSystemMessage.MessageDateTime
                    });
                }

                // Sort by last message date (newest first)
                return privateChats.OrderByDescending(c => c.LastMessageDateTime).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting private chats for user {UserId}", userId);
                return Enumerable.Empty<PrivateChatItemDto>();
            }
        }

        #endregion


    }
}

