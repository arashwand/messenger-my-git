using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;


namespace Messenger.Services.Services
{
    public class ClassGroupService : IClassGroupService
    {
        private readonly IEMessengerDbContext _context;
        private readonly IUserService _userService; // For user checks
        private readonly IManageUserService _manageUserService; // Service to create/manage users
        private readonly ILogger<ClassGroupService> _logger; // ILogger
        private readonly RedisLastMessageService _redisLastMessage;
        private readonly IRedisUnreadManage _redisUnreadManage;


        // تزریق ILogger از طریق سازنده
        public ClassGroupService(IEMessengerDbContext context, IUserService userService,
            IManageUserService manageUserService,
            ILogger<ClassGroupService> logger, RedisLastMessageService redisLastMessageService, IRedisUnreadManage redisUnreadManage)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _manageUserService = manageUserService ?? throw new ArgumentNullException(nameof(manageUserService));
            _redisLastMessage = redisLastMessageService;
            _redisUnreadManage = redisUnreadManage;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("ClassGroupService initialized."); // لاگ اولیه
        }

        public async Task<RequestUpsertClassGroupDto> CreateClassGroupAsync(RequestUpsertClassGroupDto model)
        {
            // لاگ شروع متد با پارامترهای کلیدی
            _logger.LogInformation("Attempting to create class group for TeacherUserId: {TeacherUserId}, LevelName: {LevelName}", model.TeacherUserId, model.LevelName);

            try
            {
                //ایدی دبیر را اختیاری کردیم
                // 1. Validate teacher user exists and has permission (using _userService)
                //if (!await _userService.UserExistsAndHasRoleAsync(model.TeacherUserId, "Teacher")) // مثال
                //{
                //    _logger.LogWarning("CreateClassGroupAsync: Teacher user {TeacherUserId} not found or does not have permission.", model.TeacherUserId);
                //    throw new Exception($"Teacher user {model.TeacherUserId} not found or does not have permission."); // یا بازگرداندن نتیجه مناسب
                //}

                // 2. Create ClassGroups entity
                var classGroupEntity = new ClassGroup
                {
                    ClassId = model.ClassId,
                    TeacherUserId = model.TeacherUserId,
                    LevelName = model.LevelName,
                    ClassTiming = model.ClassTiming,
                    EndDate = model.EndDate, // Nullable, handled later
                    IsActive = model.IsActive, // Default to active? Consider setting a default if needed
                                               // LeftSes = model.LeftSes // Default sessions? Consider setting a default
                };

                _logger.LogDebug("Creating ClassGroup entity in context.");
                _context.ClassGroups.Add(classGroupEntity);
                await _context.SaveChangesAsync(); // Save to get ClassId
                _logger.LogInformation("ClassGroup entity created with ClassId: {ClassId}", classGroupEntity.ClassId);

                // 3. Add teacher as the first member (UserClassGroup)
                if (model.TeacherUserId > 0)
                {
                    var memberEntity = new UserClassGroup
                    {
                        ClassId = classGroupEntity.ClassId, // Use the generated ClassId
                        UserId = model.TeacherUserId
                    };

                    _logger.LogDebug("Adding teacher {TeacherUserId} as the first member to ClassGroup {ClassId}.", model.TeacherUserId, classGroupEntity.ClassId);
                    _context.UserClassGroups.Add(memberEntity);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Teacher {TeacherUserId} added as member to ClassGroup {ClassId}.", model.TeacherUserId, classGroupEntity.ClassId);

                }

                // If EndDate is null, set it to 3 months from now
                var endClassDate = classGroupEntity.EndDate ?? DateTime.UtcNow.AddMonths(3); // Use UtcNow for consistency

                _logger.LogInformation("Successfully created ClassGroup with ClassId: {ClassId}", classGroupEntity.ClassId);

                // Map back to DTO
                return new RequestUpsertClassGroupDto
                {
                    ClassId = classGroupEntity.ClassId,
                    TeacherUserId = classGroupEntity.TeacherUserId,
                    LevelName = classGroupEntity.LevelName,
                    ClassTiming = classGroupEntity.ClassTiming,
                    EndDate = endClassDate, // Use calculated end date
                    IsActive = classGroupEntity.IsActive,
                    // LeftSes = classGroupEntity.LeftSes.Value
                };
            }
            catch (Exception ex)
            {
                // لاگ خطا با جزئیات
                _logger.LogError(ex, "Error creating class group for TeacherUserId: {TeacherUserId}, LevelName: {LevelName}", model.TeacherUserId, model.LevelName);
                // پرتاب مجدد خطا یا بازگرداندن نتیجه خطا
                throw;
            }
        }

        public async Task<RequestUpsertClassGroupDto> UpsertClassGroupAsync(RequestUpsertClassGroupDto model)
        {
            _logger.LogInformation("Upsert ClassGroup: ClassId={ClassId}, TeacherUserId={TeacherUserId}", model.ClassId, model.TeacherUserId);

            try
            {
                //ایدی دبیر را اختیاری کردیم
                // بررسی ایا ایدی ارسالی دبیر، نقش دبیر را دارد یا خیر
                //if (!await _userService.UserExistsAndHasRoleAsync(model.TeacherUserId, "Teacher")) // مثال
                //{
                //    _logger.LogWarning("CreateClassGroupAsync: Teacher user {TeacherUserId} not found or does not have permission.", model.TeacherUserId);
                //    throw new Exception($"Teacher user {model.TeacherUserId} not found or does not have permission."); // یا بازگرداندن نتیجه مناسب
                //}


                ClassGroup? existingGroup = null;

                if (model.ClassId > 0)
                {
                    existingGroup = await _context.ClassGroups
                        .Include(c => c.UserClassGroups) // برای بررسی عضویت
                        .FirstOrDefaultAsync(c => c.ClassId == model.ClassId);
                }

                if (existingGroup == null)
                {
                    // ---------- Create ----------
                    var newGroup = new ClassGroup
                    {
                        TeacherUserId = model.TeacherUserId,
                        LevelName = model.LevelName,
                        ClassTiming = model.ClassTiming,
                        EndDate = model.EndDate,
                        IsActive = model.IsActive
                    };

                    await _context.ClassGroups.AddAsync(newGroup);

                    if (model.TeacherUserId > 0)
                    {
                        // افزودن معلم به عنوان عضو 
                        newGroup.UserClassGroups = new List<UserClassGroup>
                        {
                            new UserClassGroup { UserId = model.TeacherUserId }
                        };

                        await _context.SaveChangesAsync();
                    }

                    model.ClassId = newGroup.ClassId;

                    _logger.LogInformation("ClassGroup Created: ClassId={ClassId}", newGroup.ClassId);
                }
                else
                {
                    // ---------- Update ----------
                    existingGroup.TeacherUserId = model.TeacherUserId;
                    existingGroup.LevelName = model.LevelName;
                    existingGroup.ClassTiming = model.ClassTiming;
                    existingGroup.IsActive = model.IsActive;
                    existingGroup.EndDate = model.EndDate;

                    if (model.TeacherUserId > 0)
                    {
                        // اطمینان از اینکه معلم عضو هست
                        if (!existingGroup.UserClassGroups.Any(x => x.UserId == model.TeacherUserId))
                        {
                            existingGroup.UserClassGroups.Add(new UserClassGroup
                            {
                                ClassId = existingGroup.ClassId,
                                UserId = model.TeacherUserId
                            });
                        }
                    }                    

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("ClassGroup Updated: ClassId={ClassId}", existingGroup.ClassId);
                }

                return model;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while Upsert ClassGroup for TeacherUserId={TeacherUserId}", model.TeacherUserId);
                throw;
            }
        }

        /// <summary>
        /// آپ‌سرت گروه بر اساس مدل Portal (ClassGroupModel)
        /// توضیحات:
        /// - این متد یک مدل حاوی اطلاعات گروه و اعضا (Members) دریافت می‌کند.
        /// - ابتدا کاربران (استاد و اعضا) را بررسی و در صورت عدم وجود ایجاد می‌کند.
        /// - سپس گروه را ایجاد یا بروزرسانی می‌کند.
        /// - سپس اعضای گروه را همگام‌سازی می‌کند: اعضای جدید اضافه می‌شوند و اعضایی که در ورودی نیستند حذف می‌شوند.
        /// - معلم (TeacherUserId) همیشه به عنوان عضو تضمین می‌شود.
        /// - تمام عملیات در یک تراکنش دیتابیس انجام می‌شود تا اتمی باشد.
        /// </summary>
        /// <param name="model">مدل حاوی مشخصات گروه و لیست اعضا</param>
        public async Task UpsertClassGroupFromModelAsync(ClassGroupModel model)
        {
            _logger.LogInformation("UpsertClassGroupFromModel: ClassId={ClassId}, TeacherUserId={TeacherUserId}, MembersCount={Count}", model.ClassId, model.TeacherUserId, model.Members?.Count ?? 0);

            try
            {
                // شروع تراکنش برای اطمینان از اتمیک بودن عملیات
                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 1) ابتدا کاربران را بررسی و ایجاد کنیم (استاد و اعضا)
                    // بررسی و ایجاد استاد
                    if (model.TeacherUserId > 0)
                    {
                        var teacher = await _userService.GetUserByIdAsync(model.TeacherUserId);
                        if (teacher == null)
                        {
                            var newTeacher = new UserDto
                            {
                                UserId = model.TeacherUserId,
                                NameFamily = model.TeacherName,
                                RoleName = ConstRoles.Teacher,
                                RoleFaName = RoleFaName.GetRoleName(ConstRoles.Teacher),
                                DeptName = "" // مقدار پیش‌فرض
                            };
                            await _manageUserService.CreateUserAsync(newTeacher);
                            _logger.LogInformation("Created teacher user {UserId}", model.TeacherUserId);
                        }

                        // 2) اعتبارسنجی معلم: بررسی می‌کنیم که TeacherUserId موجود باشد و نقش "Teacher" داشته باشد
                        if (!await _userService.UserExistsAndHasRoleAsync(model.TeacherUserId, ConstRoles.Teacher))
                        {
                            _logger.LogWarning("Teacher {TeacherUserId} not found or not a teacher.", model.TeacherUserId);
                            throw new KeyNotFoundException($"Teacher {model.TeacherUserId} not found or not a teacher.");
                        }

                    }
                   

                    // بررسی و ایجاد اعضا
                    if (model.Members != null)
                    {
                        foreach (var member in model.Members)
                        {
                            if (member.UserId != model.TeacherUserId) // استاد قبلاً بررسی شده
                            {
                                var user = await _userService.GetUserByIdAsync(member.UserId);
                                if (user == null)
                                {
                                    var newUser = new UserDto
                                    {
                                        UserId = member.UserId,
                                        NameFamily = member.NameFamily,
                                        RoleName = member.RoleName,
                                        RoleFaName = member.RoleFaName,
                                        DeptName = member.DeptName
                                    };
                                    await _manageUserService.CreateUserAsync(newUser);
                                    _logger.LogInformation("Created member user {UserId}", member.UserId);
                                }
                            }
                        }
                    }


                    // 3) جستجوی گروه موجود همراه با اعضای فعلی (UserClassGroups)
                    var existing = await _context.ClassGroups
                        .Include(c => c.UserClassGroups)
                        .FirstOrDefaultAsync(c => c.ClassId == model.ClassId);

                    if (existing == null)
                    {
                        // 4) اگر گروه وجود نداشت => ایجاد گروه جدید با فیلدهای دریافتی

                        existing = new ClassGroup
                        {
                            ClassId = model.ClassId,
                            TeacherUserId = model.TeacherUserId,
                            LevelName = model.LevelName,
                            ClassTiming = model.ClassTiming,
                            EndDate = model.EndDate,
                            IsActive = model.IsActive
                        };

                        _context.ClassGroups.Add(existing);
                        await _context.SaveChangesAsync(); // ذخیره برای دریافت ClassId جدید

                        _logger.LogInformation("Created new ClassGroup {ClassId}", existing.ClassId);
                    }
                    else
                    {
                        // 5) اگر گروه وجود داشت => بروزرسانی فیلدهای گروه
                        existing.TeacherUserId = model.TeacherUserId;
                        existing.LevelName = model.LevelName;
                        existing.ClassTiming = model.ClassTiming;
                        existing.EndDate = model.EndDate;
                        existing.IsActive = model.IsActive;
                        existing.ClassId = model.ClassId;

                        _context.ClassGroups.Update(existing);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Updated ClassGroup {ClassId}", existing.ClassId);
                    }

                    // 6) همگام‌سازی اعضا
                    // - استخراج شناسه اعضای ورودی بدون تکرار
                    var inputMemberIds = (model.Members ?? new List<MemberModel>()).Select(m => (long)m.UserId).ToHashSet();

                    // - اطمینان از اینکه معلم در لیست اعضا وجود دارد
                    inputMemberIds.Add(model.TeacherUserId);

                    // - استخراج شناسه اعضای فعلی از UserClassGroups
                    var currentMembers = existing.UserClassGroups.Select(uc => uc.UserId).ToHashSet();

                    // 6.a) اعضایی که باید اضافه شوند = اعضای ورودی منهای اعضای فعلی
                    var toAdd = inputMemberIds.Except(currentMembers).ToList();
                    foreach (var uid in toAdd)
                    {
                        // افزودن رکورد جدید در جدول UserClassGroups
                        _context.UserClassGroups.Add(new UserClassGroup { ClassId = existing.ClassId, UserId = uid });
                    }

                    // 6.b) اعضایی که باید حذف شوند = اعضای فعلی منهای اعضای ورودی
                    var toRemove = currentMembers.Except(inputMemberIds).ToList();
                    if (toRemove.Any())
                    {
                        // حذف رکوردهای مربوطه از جدول UserClassGroups
                        var entitiesToRemove = await _context.UserClassGroups.Where(uc => uc.ClassId == existing.ClassId && toRemove.Contains(uc.UserId)).ToListAsync();
                        if (entitiesToRemove.Any()) _context.UserClassGroups.RemoveRange(entitiesToRemove);
                    }

                    // 7) ذخیره تغییرات و commit تراکنش
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch (Exception ex)
                {
                    // لاگ خطا و تلاش برای rollback تراکنش
                    _logger.LogError(ex, "Error in UpsertClassGroupFromModel");
                    try { await tx.RollbackAsync(); } catch { }
                    throw;
                }
            }
            catch (Exception ex)
            {

                throw;
            }


        }


        public async Task<ClassGroupDto?> GetClassGroupByIdAsync(long userId, long classId)
        {
            _logger.LogInformation("Getting class group by ID: {ClassId}", classId);

            try
            {
                var user = await _userService.GetUserByIdAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning("User with ID: {UserId} not found.", userId);
                    throw new KeyNotFoundException($"User with ID {userId} not found.");
                }

                if (user.RoleName != ConstRoles.Manager && user.RoleName != ConstRoles.Personel)
                {
                    // Check if user is a member of the class group
                    var isMember = await IsUserMemberOfClassGroupAsync(userId, classId);
                    if (!isMember)
                    {
                        _logger.LogWarning("User {UserId} does not have access to class group {ClassId}.", userId, classId);
                        throw new UnauthorizedAccessException("User does not have access to this class group.");
                    }
                }

                // Fetch ClassGroups entity from DB
                var classGroupEntity = await _context.ClassGroups.FindAsync(classId);

                if (classGroupEntity == null)
                {
                    _logger.LogWarning("Class group with ID: {ClassId} not found.", classId);
                    return null; // بازگرداندن null اگر پیدا نشد
                }

                _logger.LogDebug("Class group found: {ClassId}", classId);

                // If EndDate is null, set it to 3 months from now
                var endDate = classGroupEntity.EndDate ?? DateTime.UtcNow.AddMonths(3); // Use UtcNow

                // Map to DTO
                return new ClassGroupDto
                {
                    ClassId = classGroupEntity.ClassId,
                    LevelName = classGroupEntity.LevelName,
                    TeacherUserId = classGroupEntity.TeacherUserId, // Get the actual teacher ID
                    ClassTiming = classGroupEntity.ClassTiming,
                    EndDate = endDate,
                    IsActive = classGroupEntity.IsActive,
                    LeftSes = 0,//classGroupEntity.LeftSes.Value
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting class group by ID: {ClassId}", classId);
                throw; // یا بازگرداندن null یا نتیجه خطا
            }
        }

        /// <summary>
        /// لیست گروههایی که کاربر در انها عضو است را بر میگرداند
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<ClassGroupDto>> GetUserClassGroupsAsync(long userId)
        {
            _logger.LogInformation("Getting class groups for user ID: {UserId}", userId);
            try
            {
                var classGroups = await _context.ClassGroups
                    .Include(c => c.UserClassGroups)
                    .Where(cg => cg.IsActive && cg.UserClassGroups.Any(f => f.UserId == userId)) // فیلتر کردن بر اساس فعال بودن
                    .ToListAsync();

                if (!classGroups.Any())
                {
                    _logger.LogInformation("No class groups found for user ID: {UserId}", userId);
                    return Enumerable.Empty<ClassGroupDto>(); // بازگرداندن لیست خالی
                }

                _logger.LogInformation("Found {Count} class groups for user ID: {UserId}", classGroups.Count, userId);

                // Map to ClassGroupDto


                var result = classGroups.Select(cg => new ClassGroupDto
                {
                    ClassId = cg.ClassId,
                    TeacherUserId = cg.TeacherUserId,
                    LevelName = cg.LevelName,
                    ClassTiming = cg.ClassTiming,
                    EndDate = cg.EndDate ?? DateTime.UtcNow.AddMonths(3), // Use UtcNow
                    IsActive = cg.IsActive,
                    LeftSes = cg.LeftSes ?? 0 // Handle potential null
                }).ToList();

                //--افزودن اخرین پیام ارسال شده به گروه توسط  redis
                foreach (var item in result)
                {
                    item.LastMessage = await _redisLastMessage.GetLastMessageAsync(ConstChat.ClassGroupType, item.ClassId.ToString());
                    item.LastReadMessageId = await _redisUnreadManage.GetLastReadMessageIdAsync(userId, item.ClassId, ConstChat.ClassGroupType);
                }


                return result;
            }
            catch (Exception ex)//TODO  اینجا در لود ابتدایی برنامه خطا دارم بررسی بشه
            {
                _logger.LogError(ex, "Error getting class groups for user ID: {UserId}", userId);
                throw;
            }
        }

        public async Task UpdateClassGroupInfoAsync(RequestUpsertClassGroupDto model)
        {
            _logger.LogInformation("Attempting to update info for class group ID: {ClassId}", model.ClassId);

            try
            {
                // 1. Find ClassGroups entity
                var classGroupEntity = await _context.ClassGroups.FindAsync(model.ClassId);

                if (classGroupEntity == null)
                {
                    _logger.LogWarning("UpdateClassGroupInfoAsync: Class group with ID: {ClassId} not found.", model.ClassId);
                    // پرتاب خطا یا بازگرداندن نتیجه مناسب
                    throw new KeyNotFoundException($"Class group with ID {model.ClassId} not found.");
                }

                _logger.LogDebug("Found class group {ClassId} for update.", model.ClassId);


                // Update properties
                classGroupEntity.LevelName = model.LevelName;
                classGroupEntity.ClassTiming = model.ClassTiming;
                classGroupEntity.IsActive = model.IsActive;
                //classGroupEntity.LeftSes = model.LeftSes;
                classGroupEntity.EndDate = model.EndDate; // Allow setting EndDate
                classGroupEntity.TeacherUserId = model.TeacherUserId;

                _logger.LogDebug("Updating properties for class group {ClassId}.", model.ClassId);

                _context.ClassGroups.Update(classGroupEntity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated info for class group ID: {ClassId}", model.ClassId);
            }
            catch (KeyNotFoundException knfex) // گرفتن خطای مشخص تر
            {
                _logger.LogWarning(knfex.Message); // لاگ هشدار برای "پیدا نشد"
                throw; // پرتاب مجدد برای مدیریت در لایه بالاتر
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating info for class group ID: {ClassId}", model.ClassId);
                throw;
            }
        }

        public async Task DeleteClassGroupAsync(long classId)
        {
            //TODO: /*, int deletedByUserId */ کی این گرو را حذف کرده است؟
            _logger.LogInformation("Attempting to delete class group ID: {ClassId}", classId);

            // استفاده از تراکنش برای اطمینان از حذف کامل یا هیچ چیز
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Find ClassGroups entity
                var classGroupEntity = await _context.ClassGroups.FindAsync(classId);

                if (classGroupEntity == null)
                {
                    _logger.LogWarning("DeleteClassGroupAsync: Class group with ID: {ClassId} not found.", classId);
                    await transaction.RollbackAsync(); // بازگرداندن تراکنش
                    throw new KeyNotFoundException($"Class group with ID {classId} not found.");
                }

                _logger.LogDebug("Found class group {ClassId} for deletion.", classId);



                // Remove related entities first to avoid foreign key constraints

                // ClassGroupMessages and their MessageFiles
                // بارگیری پیام‌ها و فایل‌های مرتبط برای حذف

                byte messageType = (byte)EnumMessageType.Group;
                var groupMessages = await _context.Messages
                   .Where(cgm => cgm.OwnerId == classId && cgm.MessageType == messageType)
                   .Include(m => m.MessageFiles) // Then include MessageFiles from Message
                   .ToListAsync();

                if (groupMessages.Any())
                {
                    // استخراج MessageFiles از تمام پیام‌های مرتبط
                    var messageFiles = groupMessages
                       .SelectMany(cgm => cgm.MessageFiles) // Handle null Message
                       .ToList();

                    if (messageFiles.Any())
                    {
                        _logger.LogDebug("Removing {Count} message files related to class group {ClassId}.", messageFiles.Count, classId);
                        _context.MessageFiles.RemoveRange(messageFiles);
                        // TODO: Consider deleting actual files from storage here
                    }

                    // استخراج Messages برای حذف (اگر لازم باشد و به جای دیگری مرتبط نباشند)
                    var messages = groupMessages.Distinct().ToList();
                    if (messages.Any())
                    {
                        _logger.LogDebug("Removing {Count} messages related to class group {ClassId}.", messages.Count, classId);
                        _context.Messages.RemoveRange(messages); // Remove the Message entities themselves
                    }


                    _logger.LogDebug("Removing {Count} class group message links for class group {ClassId}.", groupMessages.Count, classId);
                    _context.Messages.RemoveRange(groupMessages); // Remove the link entries
                }


                // UserClassGroups (Memberships)
                var userClassGroupEntities = await _context.UserClassGroups
                    .Where(ucg => ucg.ClassId == classId)
                    .ToListAsync();

                if (userClassGroupEntities.Any())
                {
                    _logger.LogDebug("Removing {Count} user memberships for class group {ClassId}.", userClassGroupEntities.Count, classId);
                    _context.UserClassGroups.RemoveRange(userClassGroupEntities);
                }

                // Now remove the ClassGroup itself
                _logger.LogDebug("Removing the class group entity {ClassId}.", classId);
                _context.ClassGroups.Remove(classGroupEntity);

                // Save all changes within the transaction
                await _context.SaveChangesAsync();

                // Commit the transaction if all operations were successful
                await transaction.CommitAsync();
                _logger.LogInformation("Successfully deleted class group ID: {ClassId} and related data.", classId);

                // await Task.Delay(10); // این تاخیر احتمالاً غیر ضروری است مگر دلیل خاصی داشته باشد
            }
            catch (KeyNotFoundException knfex)
            {
                _logger.LogWarning(knfex.Message); // لاگ هشدار برای "پیدا نشد"
                                                   // تراکنش قبلاً Rollback شده یا اصلاً شروع نشده است
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting class group ID: {ClassId}. Rolling back transaction.", classId);
                // تلاش برای Rollback در صورت بروز خطا
                try
                {
                    await transaction.RollbackAsync();
                    _logger.LogInformation("Transaction rolled back for deletion of class group {ClassId}.", classId);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Error rolling back transaction for deletion of class group {ClassId}.", classId);
                }
                throw; // پرتاب مجدد خطای اصلی
            }
        }


        public async Task AddUserToClassGroupAsync(long classId, long userIdToAdd, long addedByUserId)
        {
            _logger.LogInformation("User {AddedByUserId} attempting to add user {UserIdToAdd} to class group {ClassId}.",
                addedByUserId, userIdToAdd, classId);

            try
            {
                // Validate class group exists
                var classGroupExists = await _context.ClassGroups.AnyAsync(cg => cg.ClassId == classId);
                if (!classGroupExists)
                {
                    _logger.LogWarning("AddUserToClassGroupAsync: Class group {ClassId} not found.", classId);
                    throw new KeyNotFoundException($"Class group with ID {classId} not found.");
                }

                // بررسی وجود کاربر برای اضافه شدن (استفاده از UserService)
                var userToAdd = await _userService.GetUserByIdAsync(userIdToAdd, null, true);
                if (userToAdd == null)
                {
                    _logger.LogWarning("AddUserToClassGroupAsync: User to add {UserIdToAdd} not found.", userIdToAdd);
                    throw new KeyNotFoundException($"User with ID {userIdToAdd} not found.");
                }

                // احراز هویت کاربری که دارد یک کاربر اضافه می‌کند
                var addingUser = await _userService.GetUserByIdAsync(addedByUserId, null, true);
                if (addingUser == null)
                {
                    _logger.LogWarning("AddUserToClassGroupAsync: User performing action {AddedByUserId} not found.", addedByUserId);
                    throw new KeyNotFoundException($"User performing action with ID {addedByUserId} not found.");
                }


                // Check if userIdToAdd is already a member
                var isAlreadyMember = await _context.UserClassGroups.AnyAsync(ucg => ucg.ClassId == classId && ucg.UserId == userIdToAdd);
                if (isAlreadyMember)
                {
                    // تغییر به هشدار چون خطا نیست، فقط کاربر از قبل عضو است
                    _logger.LogWarning("User {UserIdToAdd} is already a member of class group {ClassId}.", userIdToAdd, classId);
                    // throw new InvalidOperationException("User is already a member of the class group."); // یا فقط بازگشت بدون خطا
                    return; // اگر کاربر از قبل عضو است، عملیات موفقیت آمیز تلقی می‌شود
                }

                // Create UserClassGroup entity
                _logger.LogDebug("Adding user {UserIdToAdd} to class group {ClassId}.", userIdToAdd, classId);
                _context.UserClassGroups.Add(new UserClassGroup
                {
                    ClassId = classId,
                    UserId = userIdToAdd
                });
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {AddedByUserId} successfully added user {UserIdToAdd} to class group {ClassId}.", addedByUserId, userIdToAdd, classId);
            }
            catch (KeyNotFoundException knfex)
            {
                _logger.LogWarning(knfex.Message);
                throw;
            }
            catch (InvalidOperationException ioex) // گرفتن خطای مشخص تر
            {
                _logger.LogWarning(ioex.Message); // لاگ هشدار برای عملیات نامعتبر (مانند عضویت تکراری)
                throw; // پرتاب مجدد
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {UserIdToAdd} to class group {ClassId} by user {AddedByUserId}.", userIdToAdd, classId, addedByUserId);
                throw;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="classId"></param>
        /// <param name="userIdToRemove">فردی که باید حذف شود</param>
        /// <param name="removedByUserId">فردی که اقدام به حذف کاربر کرده است</param>
        /// <returns></returns>
        public async Task RemoveUserFromClassGroupAsync(long classId, long userIdToRemove, long removedByUserId)
        {
            _logger.LogInformation("User {RemovedByUserId} attempting to remove user {UserIdToRemove} from class group {ClassId}.", removedByUserId, userIdToRemove, classId);

            try
            {
                // 1. Validate class group exists
                var classGroupEntity = await _context.ClassGroups.FindAsync(classId); // Get group for permission check later
                if (classGroupEntity == null)
                {
                    _logger.LogWarning("RemoveUserFromClassGroupAsync: Class group {ClassId} not found.", classId);
                    throw new KeyNotFoundException($"Class group with ID {classId} not found.");
                }

                // Validate user to remove exists (using UserService)
                var userToRemoveExists = await _userService.GetUserByIdAsync(userIdToRemove); // فرض وجود متد UserExistsAsync
                if (userToRemoveExists == null)
                {
                    _logger.LogWarning("RemoveUserFromClassGroupAsync: User to remove {UserIdToRemove} not found.", userIdToRemove);
                    throw new KeyNotFoundException($"User to remove with ID {userIdToRemove} not found.");
                }

                // Validate user performing the action exists
                var removingUserExists = await _userService.GetUserByIdAsync(removedByUserId);
                if (removingUserExists == null)
                {
                    _logger.LogWarning("RemoveUserFromClassGroupAsync: User performing action {RemovedByUserId} not found.", removedByUserId);
                    throw new KeyNotFoundException($"User performing action with ID {removedByUserId} not found.");
                }

                //فقط نقش مدیر میتواند حذف کاربر از گروه را انجام دهد

                if (removingUserExists.RoleName != ConstRoles.Manager)
                {
                    _logger.LogWarning("User {RemovedByUserId} does not have permission to remove user {UserIdToRemove} from class group {ClassId}.", removedByUserId, userIdToRemove, classId);
                    throw new UnauthorizedAccessException("User does not have permission to remove this member.");
                }

                // معلم نمیتواند خودش را حذف کند
                if (classGroupEntity.TeacherUserId == userIdToRemove && userIdToRemove == removedByUserId)
                {
                    _logger.LogWarning("Teacher {TeacherUserId} cannot remove themselves from class group {ClassId} where they are the teacher.", userIdToRemove, classId);
                    throw new InvalidOperationException("The teacher cannot remove themselves from the class group.");
                    // Consider logic for transferring ownership or deleting the group instead.
                }


                // 3. حذف فرد از جدول مورد نظر با ایدی کلاس مربوطه
                var memberEntity = await _context.UserClassGroups.FirstOrDefaultAsync(ucg => ucg.ClassId == classId && ucg.UserId == userIdToRemove);

                if (memberEntity == null)
                {
                    // تغییر به هشدار، چون کاربر عضو نبوده است
                    _logger.LogWarning("User {UserIdToRemove} is not a member of class group {ClassId}. No action taken.", userIdToRemove, classId);
                    // throw new KeyNotFoundException($"User {userIdToRemove} is not a member of class group {classId}.");
                    return; // عملیات موفقیت آمیز تلقی می‌شود چون کاربر عضو نیست
                }

                // 4. Remove the membership
                _logger.LogDebug("Removing membership link for user {UserIdToRemove} in class group {ClassId}.", userIdToRemove, classId);
                _context.UserClassGroups.Remove(memberEntity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {RemovedByUserId} successfully removed user {UserIdToRemove} from class group {ClassId}.", removedByUserId, userIdToRemove, classId);
            }
            catch (KeyNotFoundException knfex)
            {
                _logger.LogWarning(knfex.Message);
                throw;
            }
            catch (InvalidOperationException ioex)
            {
                _logger.LogWarning(ioex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {UserIdToRemove} from class group {ClassId} by user {RemovedByUserId}.", userIdToRemove, classId, removedByUserId);
                throw;
            }
        }


        /// <summary>
        /// بدست اوردن اعضای گروه
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<UserDto>> GetClassGroupMembersAsync(long userId, long classId)
        {
            _logger.LogInformation("Getting members for class group ID: {ClassId}", classId);
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning("User with ID: {UserId} not found.", userId);
                    throw new KeyNotFoundException($"User with ID {userId} not found.");
                }

                if (user.RoleName != ConstRoles.Manager && user.RoleName != ConstRoles.Personel)
                {
                    // Check if user is a member of the class group
                    var isMember = await IsUserMemberOfClassGroupAsync(userId, classId);
                    if (!isMember)
                    {
                        _logger.LogWarning("User {UserId} does not have access to class group {ClassId}.", userId, classId);
                        throw new UnauthorizedAccessException("User does not have access to this class group.");
                    }
                }

                return await GetMembersQuery(classId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting members for class group ID: {ClassId}", classId);
                throw;
            }
        }

        /// <summary>
        /// بدست اوردن اعضای گروه برای استفاده داخلی سرویس‌ها
        /// </summary>
        /// <param name="classId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<UserDto>> GetClassGroupMembersInternalAsync(long classId)
        {
            _logger.LogInformation("Getting members for class group ID: {ClassId}", classId);
            return await GetMembersQuery(classId);
        }

        /// <summary>
        /// دریافت تعداد اعضای گروه کلاسی
        /// </summary>
        public async Task<int> GetClassGroupMembersCountAsync(long classId)
        {
            _logger.LogInformation("Getting member count for class group ID: {ClassId}", classId);
            
            try
            {
                // Validate class group exists
                var classGroupExists = await _context.ClassGroups.AnyAsync(cg => cg.ClassId == classId);
                if (!classGroupExists)
                {
                    _logger.LogWarning("GetClassGroupMembersCountAsync: Class group {ClassId} not found.", classId);
                    return 0;
                }

                // Count members via UserClassGroup table
                var count = await _context.UserClassGroups
                    .Where(ucg => ucg.ClassId == classId)
                    .CountAsync();

                _logger.LogInformation("Class group {ClassId} has {Count} members", classId, count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting member count for class group {ClassId}", classId);
                return 0;
            }
        }

        private async Task<IEnumerable<UserDto>> GetMembersQuery(long classId)
        {
            _logger.LogInformation("Getting members for class group ID: {ClassId}", classId);
            try
            {
                // Validate class group exists
                var classGroupExists = await _context.ClassGroups.AnyAsync(cg => cg.ClassId == classId);
                if (!classGroupExists)
                {
                    _logger.LogWarning("GetClassGroupMembersAsync: Class group {ClassId} not found.", classId);
                    throw new KeyNotFoundException($"Class group with ID {classId} not found.");
                }

                // Query users who are members via UserClassGroup table
                var members = await _context.UserClassGroups
                   .Where(ucg => ucg.ClassId == classId)
                   .Include(ucg => ucg.User) // Include the related User
                   .Select(ucg => ucg.User) // Select the User entity
                   .Where(u => u != null) // اطمینان از اینکه User وجود دارد
                   .ToListAsync();


                if (!members.Any())
                {
                    _logger.LogInformation("No members found for class group ID: {ClassId}", classId);
                    return Enumerable.Empty<UserDto>();
                }

                _logger.LogInformation("Found {Count} members for class group ID: {ClassId}", members.Count, classId);

                // Map to UserDto
                return members.Select(u => new UserDto
                {
                    DeptName = u.DeptName,
                    NameFamily = u.NameFamily,
                    UserId = u.UserId,
                    ProfilePicName = u.ProfilePicName,
                    RoleFaName = u.RoleFaName,
                    RoleName = u.RoleName,
                    // اطمینان حاصل کنید که تمام فیلدهای مورد نیاز UserDto در User وجود دارند
                }).ToList();
            }
            catch (KeyNotFoundException knfex)
            {
                _logger.LogWarning(knfex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting members for class group ID: {ClassId}", classId);
                throw;
            }
        }

        /// <summary>
        /// چک کردن کاربر، ایا عضو گروه است یا خیر
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="classId"></param>
        /// <returns></returns>
        public async Task<bool> IsUserMemberOfClassGroupAsync(long userId, long classId)
        {
            _logger.LogInformation("Checking if user {UserId} is member of class group {ClassId}", userId, classId);
            try
            {
                // Check if a UserClassGroup entry exists
                var isMember = await _context.UserClassGroups.AnyAsync(ucg => ucg.UserId == userId && ucg.ClassId == classId);
                _logger.LogInformation("User {UserId} membership status in class group {ClassId}: {IsMember}", userId, classId, isMember);
                return isMember;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking membership for user {UserId} in class group {ClassId}", userId, classId);
                throw; // یا بازگرداندن false و لاگ خطا
            }
        }

        /// <summary>
        /// بدست اوردن کلاسهایی که استاد در ان تدریس میکند
        /// </summary>
        /// <param name="teacherUserId"></param>
        /// <returns></returns>
        public async Task<IEnumerable<RequestUpsertClassGroupDto>> GetTaughtClassGroupsAsync(long teacherUserId)
        {
            _logger.LogInformation("Getting class groups taught by teacher ID: {TeacherUserId}", teacherUserId);
            try
            {
                // Validate if the teacher exists (using UserService)
                var teacher = await _userService.GetUserByIdAsync(teacherUserId); // فرض وجود این متد
                if (teacher == null)
                {
                    _logger.LogWarning("GetTaughtClassGroupsAsync: Teacher with ID {TeacherUserId} not found.", teacherUserId);
                    throw new KeyNotFoundException($"Teacher with ID {teacherUserId} not found.");
                }

                //--فقط استاتید میتوانند در یک کلاس تدریس کنند. پس اگر نقش جز این بود، خطا برگردان
                if (teacher.RoleName != ConstRoles.Teacher) // یا هر نقش دیگری
                {
                    _logger.LogWarning("User {TeacherUserId} is not a teacher.", teacherUserId);
                    throw new UnauthorizedAccessException($"User {teacherUserId} is not authorized to perform this action.");
                }


                // Query ClassGroups where TeacherUserId matches
                var classGroupEntities = await _context.ClassGroups
                    .Where(cg => cg.TeacherUserId == teacherUserId)
                    .ToListAsync();

                if (!classGroupEntities.Any())
                {
                    _logger.LogInformation("No class groups found taught by teacher ID: {TeacherUserId}", teacherUserId);
                    return Enumerable.Empty<RequestUpsertClassGroupDto>();
                }

                _logger.LogInformation("Found {Count} class groups taught by teacher ID: {TeacherUserId}", classGroupEntities.Count, teacherUserId);

                // Map to ClassGroupDto
                return classGroupEntities.Select(cg => new RequestUpsertClassGroupDto
                {
                    ClassId = cg.ClassId,
                    TeacherUserId = cg.TeacherUserId,
                    LevelName = cg.LevelName,
                    ClassTiming = cg.ClassTiming,
                    EndDate = cg.EndDate ?? DateTime.UtcNow.AddMonths(3), // Use UtcNow
                    IsActive = cg.IsActive,
                    //LeftSes = cg.LeftSes ?? 0 // Handle potential null LeftSes
                }).ToList();
            }
            catch (KeyNotFoundException knfex)
            {
                _logger.LogWarning(knfex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting class groups taught by teacher ID: {TeacherUserId}", teacherUserId);
                throw;
            }
        }

        public async Task<IEnumerable<ClassGroupDto>> GetAllClassGroupsAsync()
        {
            _logger.LogInformation("Getting all active class groups.");
            try
            {
                // Query ClassGroups where IsActive is true
                var classGroupEntities = await _context.ClassGroups
                    //.Where(cg => cg.IsActive) // فیلتر کردن بر اساس فعال بودن
                    .ToListAsync();

                if (!classGroupEntities.Any())
                {
                    _logger.LogInformation("No active class groups found.");
                    return Enumerable.Empty<ClassGroupDto>();
                }

                _logger.LogInformation("Found {Count} active class groups.", classGroupEntities.Count);

                // Map to ClassGroupDto
                var result = classGroupEntities.Select(cg => new ClassGroupDto
                {
                    ClassId = cg.ClassId,
                    TeacherUserId = cg.TeacherUserId,
                    LevelName = cg.LevelName,
                    ClassTiming = cg.ClassTiming,
                    EndDate = cg.EndDate ?? DateTime.UtcNow.AddMonths(3), // Use UtcNow
                    IsActive = cg.IsActive,
                    LeftSes = cg.LeftSes ?? 0 // Handle potential null
                }).ToList();

                //--افزودن اخرین پیام ارسال شده به گروه توسط  redis
                foreach (var item in result)
                {
                    item.LastMessage = await _redisLastMessage.GetLastMessageAsync(ConstChat.ClassGroupType, item.ClassId.ToString());

                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all active class groups.");
                throw;
            }
        }


    }
}

