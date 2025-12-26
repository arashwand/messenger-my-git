// =========================================================================
// UI RENDERER MODULE
// =========================================================================
// مدیریت رندرینگ UI، ایجاد المان‌های DOM و به‌روزرسانی ظاهر
// =========================================================================

window.chatUIRenderer = (function ($) {
    'use strict';

    // =================================================
    //            PRIVATE VARIABLES & PROPERTIES
    // =================================================
    let currentUser = null;
    let currentUserNameFamily = "شما";
    let currentUserProfilePic = "UserIcon.png";
    const typingUsers = {}; // { groupId: Set(userFullName) }

    // =================================================
    //                 PRIVATE METHODS
    // =================================================

    /**
     * یک پیام را به صورت بصری به چت اضافه می‌کند.
     */
    function addMessageToUI(message, prepend = false) {
        const chatContent = $('#Message_Days');
        if (!chatContent.length) {
            console.error("Main message container (#Message_Days) not found.");
            return null;
        }

        const messageDate = new Date(message.messageDateTime);
        const dateStr = formatDate(messageDate); // اینجا استفاده شده
        const dateId = `date-${dateStr}`;

        let dateContainer = chatContent.find(`.message-box-list[data-message-date="${dateId}"]`);

        if (!dateContainer.length) {
            const persianDate = convertGregorianToJalaaliSimple(dateStr); // اینجا استفاده شده
            const newDateHeaderHtml = `<h6 class="fw-normal text-center heading chatInDateLabelClass" data-label="${persianDate}" id="${dateId}">${persianDate}</h6>`;
            const newDateContainerHtml = `<ul class="message-box-list" data-message-date="${dateId}"></ul>`;

            if (prepend) {
                chatContent.prepend(newDateContainerHtml);
                chatContent.prepend(newDateHeaderHtml);
            } else {
                chatContent.append(newDateHeaderHtml);
                chatContent.append(newDateContainerHtml);
            }
            dateContainer = chatContent.find(`.message-box-list[data-message-date="${dateId}"]`);
        }

        const $messageBody = $(createMessageHtmlBody(message));

        if (prepend) {
            dateContainer.prepend($messageBody);
        } else {
            dateContainer.append($messageBody);
        }

        return $messageBody;
    }

    // تابع formatDate که باید اضافه شود
    function formatDate(date) {
        if (window.chatUtils && window.chatUtils.formatDate) {
            return window.chatUtils.formatDate(date);
        }

        // Fallback implementation
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    // تابع convertGregorianToJalaaliSimple که باید اضافه شود
    function convertGregorianToJalaaliSimple(dateStr) {
        if (window.chatUtils && window.chatUtils.convertGregorianToJalaaliSimple) {
            return window.chatUtils.convertGregorianToJalaaliSimple(dateStr);
        }

        // Fallback implementation
        try {
            const parts = dateStr.split('-');
            if (parts.length === 3) {
                const year = parseInt(parts[0]);
                const month = parseInt(parts[1]);
                const day = parseInt(parts[2]);
                return `${year}/${month}/${day}`;
            }
        } catch (e) {
            console.error("Error in date conversion:", e);
        }
        return dateStr;
    }

    /**
     * تبدیل تاریخ به ساعت و دقیقه
     */
    function convertDateTohhmm(dateTime) {
        if (window.chatUtils && window.chatUtils.convertDateTohhmm) {
            return window.chatUtils.convertDateTohhmm(dateTime);
        }

        // Fallback implementation
        console.log('input datetime is : ' + dateTime);
        const date = new Date(dateTime);

        if (isNaN(date)) {
            return dateTime;
        }

        return date.toLocaleTimeString('en-US', {
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        });
    }

    /**
     * استخراج ساعت و دقیقه از رشته ISO
     */
    function extractTime(isoString) {
        if (window.chatUtils && window.chatUtils.extractTime) {
            return window.chatUtils.extractTime(isoString);
        }

        // Fallback implementation
        const date = new Date(isoString);
        return date.toLocaleTimeString('en-US', {
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        });
    }

    /**
     * فرمت بندی سایز فایل
     */
    function formatFileSize(bytes) {
        if (window.chatUtils && window.chatUtils.formatFileSize) {
            return window.chatUtils.formatFileSize(bytes);
        }

        // Fallback implementation
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }

    /**
     * فرمت زمان صوتی
     */
    function formatAudioTime(time) {
        if (window.chatUtils && window.chatUtils.formatAudioTime) {
            return window.chatUtils.formatAudioTime(time);
        }

        // Fallback implementation
        if (isNaN(time) || !isFinite(time)) {
            console.log('isNaN || !isFinite');
            return "0:00";
        }
        console.log('formatAudioTime : ' + time);
        const minutes = Math.floor(time / 60);
        const seconds = Math.floor(time % 60);
        return `${minutes}:${seconds.toString().padStart(2, '0')}`;
    }

    /**
     * HTML کامل یک پیام را ایجاد می‌کند.
     */
    function createMessageHtmlBody(message, edited = false) {
        const isSelf = (currentUser == message.senderUserId);
        const liClass = isSelf ? 'personal' : 'new';
        const systemClass = message.isSystemMessage ? ' systemMessage' : '';
        const elementId = message.status === 'sending' ? `message-msg-temp-${message.clientMessageId}` : `message-${message.messageId}`;
        const messageId = message.messageId || '';
        const messageDetailsJson = message.jsonMessageDetails || makeJsonObjectForMessateDetails(message); // اینجا استفاده شده

        let dropdownHtml = `
        <div class="dropdown message-options">
            <a class="btn" href="#" role="button" data-bs-toggle="dropdown" aria-expanded="false">
                <img src="/chatzy/assets/iconsax/menu-meatballs.svg" alt="menu" />
            </a>
        <div class="dropdown-menu">`;

        if (isSelf) {
            dropdownHtml += `
                <a class="dropdown-item d-flex align-items-center actionEditMessage" data-messageid="${messageId}" href="#">
                    <img src="/chatzy/assets/iconsax/edit.svg" class="svgInvertColor" alt="ویرایش" />&nbsp;
                    <span>ویرایش</span>
                </a>
                <a class="dropdown-item d-flex align-items-center actionDeleteMessage" data-messageid="${messageId}" href="#">
                    <img src="/chatzy/assets/iconsax/trash.svg" />&nbsp;
                    <span>حذف</span>
                </a>`;
        }

        dropdownHtml += `
            <a class="dropdown-item d-flex align-items-center actionReplyMessage" data-messageid="${messageId}" href="#">
                <img src="/chatzy/assets/iconsax/redo-arrow.svg" class="svgInvertColor" />&nbsp;
                <span>پاسخ دادن</span>
            </a>
            <a class="dropdown-item d-flex align-items-center actionSaveMessage" data-messageid="${messageId}" href="#">
                <img src="/chatzy/assets/iconsax/save-2.svg" class="svgInvertColor" />&nbsp;
                <span>ذخیره</span>
            </a>`;

        // Pin/unpin: only show when current user role is allowed
        try {
            const userRole = $('#userRoleName').val() || '';
            const allowedRoles = ['Teacher', 'Personel', 'Manager'];
            if (allowedRoles.includes(userRole)) {
                const isPinned = !!message.isPin;
                const pinText = isPinned ? 'لغو سنجاق' : 'سنجاق';
                const pinIcon = '/chatzy/assets/iconsax/pin-1.svg';
                dropdownHtml += `
                <a class="dropdown-item d-flex align-items-center actionPinMessage" data-messageid="${messageId}" data-is-pinned="${isPinned ? 'true' : 'false'}" href="#" aria-label="Pin message">
                    <img src="${pinIcon}" class="svgInvertColor" />&nbsp;
                    <span class="pin-text">${pinText}</span>
                </a>`;
            }
        } catch (err) {
            console.warn('Error while determining user role for pin option:', err);
        }

        dropdownHtml += `</div></div>`;

        let replyPreviewHtml = '';
        if (message.replyToMessageId && message.replyMessage) {
            replyPreviewHtml = `<div class="reply-preview border p-2 rounded bg-light mb-2" style="cursor:pointer;" data-reply-to-id="${message.replyToMessageId}">
                                <div class="text-muted small">پاسخ به: <strong>${message.replyMessage.senderUserName}</strong></div>
                                <div class="text-truncate">${message.replyMessage.messageText || ''}</div>
                            </div>`;
        }

        let filesHtml = '';
        if (message.messageFiles && message.messageFiles.length > 0) {
            filesHtml += '<div class="row mt-1 overflow-hidden">';
            message.messageFiles.forEach(file => { filesHtml += createDisplayFileBody(file, isSelf); });
            filesHtml += '</div>';
        }

        const messageTextHtml = message.messageText ? message.messageText.replace(/\n/g, '<br />') : '';
        const editedIndicator = edited ? ` <small class="text-muted fst-italic">(ویرایش شده)</small>` : '';

        let senderName = '';
        let timingHtml = `<div class="timing"><h6>${convertDateTohhmm(message.messageDateTime)}</h6>`; // اینجا استفاده شده

        if (isSelf) {
            if (message.status === 'sending') {
                timingHtml += '🕒';
            } else {
                timingHtml += `<img class="img-fluid tick" src="/chatzy/assets/images/svg/tick.svg" alt="tick" style="display: ${message.isReadByAnyRecipient ? "none" : "inline"};">
                           <img class="img-fluid tick-all" src="/chatzy/assets/images/svg/tick-all.svg" alt="tick" style="display: ${message.isReadByAnyRecipient ? "inline" : "none"};">`;
            }
        }

        if (!isSelf) {
            timingHtml += `<h6>${message.senderUserName}</h6>`;
        }

        timingHtml += '</div>';

        let personImageHtml = '';
        if (!isSelf) {
            personImageHtml = `<img class="img-fluid person-img" src="/assets/media/avatar/${message.profilePicName || 'UserIcon.png'}" alt="p9">`;
        }

        return `
        <li class="message ${liClass}${systemClass}" id="${elementId}" data-message-id="${messageId}" data-client-id="${message.clientMessageId || ''}" data-sender-id="${message.senderUserId}" data-sender-username="${message.senderUserName}" data-message-details='${messageDetailsJson}' data-is-read="${message.isReadByAnyRecipient ? 'true' : 'false'}">
            ${dropdownHtml}
            <div class="message-box ${message.isReadByAnyRecipient ? "read" : ""}">
                ${personImageHtml}
                <div class="message-box-details">
                    ${replyPreviewHtml}
                    <h5>${messageTextHtml}${editedIndicator}</h5>
                    ${filesHtml}
                    ${timingHtml}
                    ${senderName}
                </div>
            </div>
        </li>`;
    }

    // تابع makeJsonObjectForMessateDetails که باید اضافه شود
    function makeJsonObjectForMessateDetails(message) {
        if (window.chatUtils && window.chatUtils.makeJsonObjectForMessateDetails) {
            return window.chatUtils.makeJsonObjectForMessateDetails(message);
        }

        // Fallback implementation
        try {
            console.log('inside makeJsonObjectForMessateDetails ******************************' + message);

            if (!message) {
                return JSON.stringify({});
            }

            const messageDetails = {
                messageText: message.messageText || '',
                replyToMessageId: message.replyToMessageId || null,
                replyMessage: message.replyMessage || null,
                messageFiles: message.messageFiles || [],
            };

            const messageDetailsJson = JSON.stringify(messageDetails);
            console.log("JSON object created successfully:", messageDetailsJson);

            return messageDetailsJson;
        } catch (error) {
            console.error("An error occurred:", error.message);
            return JSON.stringify({});
        }
    }

    /**
     * HTML برای نمایش فایل‌ها ایجاد می‌کند.
     */
    function createDisplayFileBody(file, isSelf, isReplyed = null) {
        const fileExtension = file.fileName.split('.').pop().toLowerCase();
        console.log('file record extention is : ' + fileExtension);
        var fileHtml = "";

        const baseUrl = $('#baseUrl').val() || '';
        let path = file.fileThumbPath || file.filePath || '';
        const finalPath = path.startsWith('blob:') ? path : baseUrl + path;

        if (window.chatApp && window.chatApp.ALLOWED_IMAGES && window.chatApp.ALLOWED_IMAGES.includes(fileExtension)) {
            const imageWidth = isReplyed ? '50' : '100';
            const baseUrl = $('#baseUrl').val() || '';
            const thumbnailUrl = `/api/Chat/downloadThumbnailById?messageFileId=${file.messageFileId}`;
            fileHtml = `
                <div class="col file-attachment-item" data-file-id="${file.messageFileId}" style="display: flex; flex-direction: column;">
                   <img class="img-thumbnail chat-thumbnail"  src="${thumbnailUrl}" data-original-filename="${file.originalFileName || file.fileName}" alt="${file.fileName}" style="max-height:150px; cursor:pointer;">
                </div>`;
        }
        else if (fileExtension === 'webm') {
            const isBlob = path.startsWith('blob:');
            if (isSelf === 'self' || isBlob) {
                fileHtml = `
            <div class="col file-attachment-item audio-attachment" data-file-id="${file.messageFileId}">
                <div class="audio-player-container">
                    <button class="voice-playback-btn"><i class="iconsax" data-icon="play"></i></button>
                    <div class="voice-timeline-container">
                        <div class="voice-timeline-bg"></div>
                        <div class="voice-timeline-progress"></div>
                        <div class="voice-timeline-handle"></div>
                    </div>
                    <div class="voice-duration-display">0:00</div>
                    <audio class="d-none" src="${finalPath}" preload="metadata"></audio>
                </div>
            </div>`;
            }
            else {
                const fileSize = formatFileSize(file.fileSize); // اینجا استفاده شده
                fileHtml = `
                 <div class="col file-attachment-item audio-attachment" data-file-id="${file.messageFileId}">
                  <div class="audio-player-container" data-file-id="${file.messageFileId}">
                    <button class="voice-playback-btn">
                        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="#000"><g clip-path="url(#clip0_4418_9259)"><path d="M4 12.0004V8.44038C4 4.02038 7.13 2.21038 10.96 4.42038L14.05 6.20038L17.14 7.98038C20.97 10.1904 20.97 13.8104 17.14 16.0204L14.05 17.8004L10.96 19.5804C7.13 21.7904 4 19.9804 4 15.5604V12.0004Z" stroke="#fff" stroke-width="1.5" stroke-miterlimit="10" stroke-linecap="round" stroke-linejoin="round"></path></g><defs><clipPath id="clip0_4418_9259"><rect width="24" height="24" fill="white"></rect></clipPath></defs></svg>
                    </button>
                    <div class="voice-timeline-container">
                        <div class="voice-timeline-bg"></div>
                        <div class="voice-timeline-progress"></div>
                        <div class="voice-timeline-handle"></div>
                    </div>
                    <div class="voice-duration-display">0:00</div>
                    <audio class="d-none" preload="metadata"></audio>
                </div>   
                 </div>`;
            }
        }
        else {
            const displayName = file.originalFileName || file.fileName || 'فایل پیوست';
            const fileSizeText = file.fileSize ? formatFileSize(file.fileSize) : ''; // اینجا استفاده شده
            const cleanFileSizeText = fileSizeText.includes('NaN') ? '' : fileSizeText;

            fileHtml = `
            <div class="col file-attachment-item" data-file-id="${file.messageFileId}" style="display: flex; flex-direction: column;">
                    <i class="iconsax" data-icon="document-text-1" style="font-size: 3em;" aria-hidden="true"></i>
                    ${displayName}
                    <span style="min-width:75px;" class="btn-download-file" data-file-id="${file.messageFileId}" data-file-originalName="${file.originalFileName}">
                        <small class="d-block text-muted">${cleanFileSizeText}</small>
                        <img src="/chatzy/assets/iconsax/download.svg" class="download-icon" style="cursor:pointer; margin-top: 5px; width: 24px; height: 24px;" alt="download">
                        <img src="/chatzy/assets/iconsax/spinner.svg" class="spinner-icon" style="display: none; width: 24px; height: 24px;" alt="loading">
                    </span>
            </div>`;
        }
        return fileHtml;
    }

    /**
     * پیام‌ها را بر اساس تاریخ گروه‌بندی کرده و به UI اضافه می‌کند.
     */
    function groupMessagesByDate(messages, prepend = true) {
        if (!messages || messages.length === 0) return;

        messages.sort((a, b) => a.messageId - b.messageId);

        if (prepend) {
            for (let i = messages.length - 1; i >= 0; i--) {
                addMessageToUI(messages[i], true);
            }
        } else {
            for (let i = 0; i < messages.length; i++) {
                addMessageToUI(messages[i], false);
            }
        }

        if (typeof init_iconsax === 'function') {
            init_iconsax();
        }
    }

    /*================================متد نمایش پیام==================================*/
    /**
 * نمایش پیام جدید در UI
 */
    function displayMessage(message) {
        logMessageReceived(message);

        const activeGroupId = parseInt($('#current-group-id-hidden-input').val());
        const currentUserId = currentUser || parseInt($('#userId').val());
        const isSelf = determineIfMessageIsSelf(message, currentUserId);

        // ۱. همیشه پیش‌نمایش سایدبار را آپدیت کن
        updateSidebarPreview(message);

        // ۲. تشخیص اینکه آیا پیام باید در چت فعال نمایش داده شود
        const shouldDisplay = shouldDisplayInActiveChat(message, activeGroupId);

        if (shouldDisplay) {
            displayMessageInActiveChat(message, isSelf, currentUserId);
        } else if (!isSelf) {
            updateUnreadBadgeForInactiveChat(message);
        }
    }

    // =================================================
    //         HELPER FUNCTIONS FOR displayMessage
    // =================================================

    /**
     * لاگ اطلاعات دریافت پیام
     */
    function logMessageReceived(message) {
        console.log("📩 Displaying message received:", message);
        console.log(`   message.groupId: ${message.groupId}`);
        console.log(`   message.groupType: ${message.groupType}`);
        console.log(`   message.chatKey: ${message.chatKey}`);
        console.log(`   window.activeGroupId: ${window.activeGroupId}`);
        console.log(`   #current-group-id-hidden-input: ${$('#current-group-id-hidden-input').val()}`);
    }

    /**
     * تشخیص اینکه آیا پیام از طرف خود کاربر است
     */
    function determineIfMessageIsSelf(message, currentUserId) {
        if (message.isSystemMessage) {
            message.senderUserName = "systembot";
            return false;
        }
        return currentUserId === message.senderUserId;
    }

    /**
     * به‌روزرسانی پیش‌نمایش آخرین پیام در سایدبار
     */
    function updateSidebarPreview(message) {
        const chatTextElement = document.getElementById(`chatText_${message.groupType}_${message.groupId}`);
        const chatTimeElement = document.getElementById(`chatTime_${message.groupType}_${message.groupId}`);

        if (chatTextElement && chatTimeElement) {
            const previewText = createMessagePreviewText(message);
            chatTextElement.innerHTML = `<span>${message.senderUserName}:</span> ${previewText}`;
            chatTimeElement.innerText = convertDateTohhmm(message.messageDateTime);

            const listItem = document.getElementById(`chatListItem_${message.groupId}`);
            if (listItem) {
                listItem.parentElement.prepend(listItem);
            }
        }
    }

    /**
     * تشخیص اینکه آیا پیام باید در چت فعال نمایش داده شود
     */
    function shouldDisplayInActiveChat(message, activeGroupId) {
        if (message.groupType === 'Private') {
            // برای چت خصوصی:  مقایسه chatKey
            if (window.activeGroupId && message.chatKey) {
                const shouldDisplay = (window.activeGroupId === message.chatKey);
                console.log(`✅ Private chat check: ${window.activeGroupId} === ${message.chatKey} → ${shouldDisplay}`);
                return shouldDisplay;
            } else {
                // Fallback:  مقایسه groupId
                const shouldDisplay = (message.groupId === activeGroupId);
                console.log(`⚠️ Fallback Private chat check: ${message.groupId} === ${activeGroupId} → ${shouldDisplay}`);
                return shouldDisplay;
            }
        } else {
            // برای گروه‌ها و کانال‌ها
            const shouldDisplay = (message.groupId === activeGroupId);
            console.log(`Group/Channel check: ${message.groupId} === ${activeGroupId} → ${shouldDisplay}`);
            return shouldDisplay;
        }
    }

    /**
     * نمایش پیام در چت فعال
     */
    function displayMessageInActiveChat(message, isSelf, currentUserId) {
        console.log('✅ Displaying message in active chat');

        const chat_content = $('#chat_content');
        if (!chat_content.length) {
            console.error("Chat content container not found.");
            return;
        }

        // پیدا کردن یا ایجاد کانتینر تاریخ
        const messageDate = new Date(message.messageDate);
        const dateStr = formatDate(messageDate);
        let messageList = findOrCreateDateContainer(dateStr, messageDate);

        if (!messageList || !messageList.length) {
            console.error("Could not find or create message list container.");
            return;
        }

        // ذخیره وضعیت اسکرول قبل از اضافه کردن پیام
        const scrollState = captureScrollState(chat_content);

        // اضافه کردن پیام به DOM
        const msgHtml = createMessageHtmlBody(message);
        const $msgElement = $(msgHtml);
        messageList.append($msgElement);

        // راه‌اندازی مجدد آیکون‌ها
        if (typeof init_iconsax === 'function') {
            init_iconsax();
        }

        // مدیریت اسکرول
        handleScrollAfterMessage(chat_content, scrollState, isSelf);

        // بررسی وضعیت خوانده شدن
        if (!isSelf) {
            scheduleVisibilityCheck($msgElement);
        }
    }

    /**
     * پیدا کردن یا ایجاد کانتینر برای تاریخ مشخص
     */
    function findOrCreateDateContainer(dateStr, messageDate) {
        let messageList = $(`#chatMessages-${dateStr}`);

        if (!messageList.length) {
            const dateLabel = generateDateLabel(messageDate, dateStr);
            const headerId = `date-${dateStr}`;
            const newDayHtml = `
            <h6 class="fw-normal text-center heading chatInDateLabelClass" data-label="${dateLabel}" id="${headerId}">${dateLabel}</h6>
            <ul class="message-box-list" id="chatMessages-${dateStr}"></ul>`;

            $('#Message_Days').append(newDayHtml);
            messageList = $(`#chatMessages-${dateStr}`);
        }

        return messageList;
    }

    /**
     * تولید برچسب تاریخ (امروز، دیروز، یا تاریخ کامل)
     */
    function generateDateLabel(messageDate, dateStr) {
        const today = new Date();
        const yesterday = new Date();
        yesterday.setDate(yesterday.getDate() - 1);

        const todayStr = formatDate(today);
        const yesterdayStr = formatDate(yesterday);

        if (dateStr === todayStr) {
            return "امروز";
        } else if (dateStr === yesterdayStr) {
            return "دیروز";
        } else {
            return messageDate.toLocaleDateString('fa-IR', {
                weekday: 'long',
                year: 'numeric',
                month: 'long',
                day: 'numeric'
            });
        }
    }

    /**
     * ذخیره وضعیت اسکرول فعلی
     */
    function captureScrollState(chat_content) {
        const scrollHeight = chat_content.prop("scrollHeight");
        const scrollTop = chat_content.scrollTop();
        const clientHeight = chat_content.innerHeight();
        const wasAtBottom = (scrollHeight - (scrollTop + clientHeight)) <= 30;

        return {
            scrollHeight,
            scrollTop,
            clientHeight,
            wasAtBottom
        };
    }

    /**
     * مدیریت اسکرول بعد از اضافه کردن پیام
     */
    function handleScrollAfterMessage(chat_content, scrollState, isSelf) {
        if (isSelf || scrollState.wasAtBottom) {
            // اسکرول به پایین برای پیام‌های خودی یا زمانی که کاربر در انتها بود
            requestAnimationFrame(() => {
                const chatFinished = $('#chat-finished');
                chatFinished[0]?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                $('#newMessagesNotice').hide().data('newCount', 0).text('');
            });
        } else {
            // نمایش اعلان "پیام جدید"
            const newNotice = $('#newMessagesNotice');
            let count = newNotice.data('newCount') || 0;
            count++;
            newNotice.data('newCount', count).text(`مشاهده ${count} پیام جدید`).show();
        }
    }

    /**
     * برنامه‌ریزی بررسی دیده شدن پیام
     */
    function scheduleVisibilityCheck($msgElement) {
        setTimeout(() => {
            if (window.chatMessageManager && window.chatMessageManager.checkVisibleMessages) {
                window.chatMessageManager.checkVisibleMessages($msgElement);
            }
        }, 250);
    }

    /**
     * به‌روزرسانی badge برای چت غیرفعال
     */
    function updateUnreadBadgeForInactiveChat(message) {
        console.log('❌ Message NOT in active chat - updating unread badge');

        const badgeKey = generateBadgeKey(message);
        console.log(`   Looking for badge with key: ${badgeKey}`);

        const unreadBadge = $(`#unreadCountBadge_${badgeKey}`);

        if (unreadBadge.length) {
            let currentCount = parseInt(unreadBadge.text()) || 0;
            currentCount++;
            unreadBadge.text(currentCount).removeClass('d-none');
            console.log(`✅ Updated badge for ${badgeKey}: ${currentCount}`);
        } else {
            console.warn(`⚠️ Badge not found for key: ${badgeKey}`);
            console.warn(`   Tried selector: #unreadCountBadge_${badgeKey}`);
        }
    }

    /**
     * تولید کلید badge بر اساس نوع پیام
     */
    function generateBadgeKey(message) {
        if (message.groupType === 'Private' && message.chatKey) {
            return message.chatKey; // مثلاً "private_5_124644"
        } else {
            return `${message.groupType}_${message.groupId}`; // مثلاً "ClassGroup_10"
        }
    }

    /*================================پایان متد نمایش پیام============================*/

    /**
     * یک متن خلاصه‌ و مناسب برای نمایش در پیش‌نمایش لیست چت‌ها ایجاد می‌کند.
     */
    function createMessagePreviewText(message) {
        if (message.messageText && message.messageText.trim() !== '') {
            return message.messageText;
        }

        if (message.messageFiles && message.messageFiles.length > 0) {
            const firstFile = message.messageFiles[0];
            const fileName = firstFile.originalFileName || firstFile.fileName || '';
            const fileExtension = fileName.split('.').pop().toLowerCase();

            if (window.chatApp && window.chatApp.ALLOWED_AUDIO && window.chatApp.ALLOWED_AUDIO.includes(fileExtension)) {
                return '<i class="iconsax" data-icon="mic-2" style="margin-left: 5px;"></i> فایل ضبط شده';
            }

            if (window.chatApp && window.chatApp.ALLOWED_IMAGES && window.chatApp.ALLOWED_IMAGES.includes(fileExtension)) {
                return '<i class="iconsax" data-icon="camera" style="margin-left: 5px;"></i> عکس';
            }

            const truncatedName = fileName.length > 20
                ? fileName.substring(0, 18) + '...'
                : fileName;

            return `<i class="iconsax" data-icon="paperclip-2" style="margin-left: 5px;"></i> ${truncatedName}`;
        }

        return 'پیام';
    }

    /**
     * وضعیت یک پیام موجود در UI را به‌روز می‌کند.
     */
    function updateMessageStatus(clientMessageId, savedMessage, newStatus, jsonObject = null) {
        console.log('clientMessageId: ' + clientMessageId + ' newStatus:' + newStatus);
        const messageElement = $(`#message-msg-temp-${clientMessageId}`);

        if (!messageElement.length) {
            console.warn("Could not find message element to update status for:", clientMessageId);
            return;
        }

        const timingElement = messageElement.find('.timing');
        if (newStatus === 'sent') {
            messageElement.attr('id', `message-${savedMessage.messageId}`);
            messageElement.attr('data-message-id', savedMessage.messageId);
            messageElement.attr('data-message-details', jsonObject);

            const time = extractTime(savedMessage.messageDateTime); // اینجا استفاده شده

            if (timingElement.length) {
                timingElement.html(`
                    <h6>${time}</h6>    
                    <img class="img-fluid tick" src="/chatzy/assets/images/svg/tick.svg" alt="tick" style="display: inline;">
                    <img class="img-fluid tick-all" src="/chatzy/assets/images/svg/tick-all.svg" alt="tick" style="display: none;">
                `);
            } else {
                console.log('timingElement not found!');
            }

            const messageSenderElement = messageElement.find('.message-sender-name').last();
            if (messageSenderElement.length) {
                messageSenderElement.html(savedMessage.senderUser.nameFamily);
                console.log(savedMessage.senderUserName);
            } else {
                console.log('messageSenderElement not found!');
            }

            const timeElement = messageElement.find('.message-date').last();
            if (timeElement.length) {
                timeElement.text(convertDateTohhmm(savedMessage.messageDateTime)); // اینجا استفاده شده
            }

            if (savedMessage.messageFiles && Array.isArray(savedMessage.messageFiles)) {
                savedMessage.messageFiles.forEach(file => {
                    console.log('------------------------------------############################' + file.messageFileId + 'fileName : ' + file.originalFileName);
                    const fileElement = messageElement.find(`.file-attachment-item[data-file-id="${file.messageFileId}"]`);
                    if (fileElement.length) {
                        if (file.fileType && file.fileType.toLowerCase().startsWith('image/')) {
                            const imgElement = fileElement.find('img');
                            const linkElement = fileElement.find('a.popup-media');
                            if (imgElement.length && file.url) {
                                imgElement.attr('src', file.url);
                                imgElement.attr('alt', file.fileName || 'image');
                            }
                            if (linkElement.length && file.url) {
                                linkElement.attr('href', file.url);
                            }
                        }
                    } else {
                        console.warn(`File element with ID ${file.messageFileId} not found!`);
                    }
                });
            }

            messageElement.find('.dropdown-menu a').each(function () {
                $(this).attr('data-messageid', savedMessage.messageId);
            });

        }
        else if (newStatus === 'failed') {
            const timingElement = messageElement.find('.timing');
            timingElement.html('<span class="text-danger">❗</span>');
        }
    }

    /**
     * بروز رسانی پیام ویرایش شده
     */
    function updateEditMessageStatus(messageId, savedMessage, newStatus, jsonObject = null, errorMessage = null) {
        console.log('Edit messageId: ' + messageId + ' newStatus:' + newStatus);
        const messageElement = $(`#message-${messageId}`);

        if (!messageElement.length) {
            console.warn("Could not find message element to update status for:", messageId);
            return;
        }

        const timingElement = messageElement.find('.timing');
        if (newStatus === 'sent') {
            messageElement.attr('id', `message-${savedMessage.messageId}`);
            messageElement.attr('data-message-id', savedMessage.messageId);
            messageElement.attr('data-message-details', jsonObject);

            if (timingElement.length) {
                timingElement.html(`
                    <img class="img-fluid tick" src="/chatzy/assets/images/svg/tick.svg" alt="tick" style="display: inline;">
                    <img class="img-fluid tick-all" src="/chatzy/assets/images/svg/tick-all.svg" alt="tick" style="display: none;">
                `);
            } else {
                console.log('timingElement not found!');
            }

            const messageSenderElement = messageElement.find('.message-sender-name').last();
            if (messageSenderElement.length) {
                messageSenderElement.html(savedMessage.senderUser.nameFamily);
                console.log(savedMessage.senderUserName);
            } else {
                console.log('messageSenderElement not found!');
            }

            const timeElement = messageElement.find('.message-date').last();
            if (timeElement.length) {
                timeElement.text(convertDateTohhmm(savedMessage.messageDateTime)); // اینجا استفاده شده
            }

            if (savedMessage.messageFiles && Array.isArray(savedMessage.messageFiles)) {
                savedMessage.messageFiles.forEach(file => {
                    console.log('------------------------------------############################' + file.messageFileId + 'fileName : ' + file.originalFileName);
                    const fileElement = messageElement.find(`.file-attachment-item[data-file-id="${file.messageFileId}"]`);
                    if (fileElement.length) {
                        if (file.fileType && file.fileType.toLowerCase().startsWith('image/')) {
                            const imgElement = fileElement.find('img');
                            const linkElement = fileElement.find('a.popup-media');
                            if (imgElement.length && file.url) {
                                imgElement.attr('src', file.url);
                                imgElement.attr('alt', file.fileName || 'image');
                            }
                            if (linkElement.length && file.url) {
                                linkElement.attr('href', file.url);
                            }
                        }
                    } else {
                        console.warn(`File element with ID ${file.messageFileId} not found!`);
                    }
                });
            }

            messageElement.find('.dropdown-menu a').each(function () {
                $(this).attr('data-messageid', savedMessage.messageId);
            });

        }
        else if (newStatus === 'failed') {
            timingElement.html('<span class="text-danger">❗</span>');

            // نمایش پیام خطای معنادار
            const displayMessage = errorMessage || 'خطا در ویرایش پیام';
            console.log('------------------------------ failed edit message ------------------------------')
            showToast(displayMessage, 'info');
        }
    }


    /**
* نمایش پیام Toast به کاربر
* @param {string} message - متن پیام
* @param {string} type - نوع پیام: 'info' | 'danger' | 'warning' | 'success'
*/
    function showToast(message, type = 'danger') {
        console.log(`------------------------------ showToast: ${type} ------------------------------`);

        // تنظیمات رنگ برای هر نوع
        const styles = {
            danger: {
                background: "linear-gradient(to right, #dc3545, #c82333)",
                bootstrapClass: "text-bg-danger"
            },
            warning: {
                background: "linear-gradient(to right, #ffc107, #e0a800)",
                bootstrapClass: "text-bg-warning"
            },
            info: {
                background: "linear-gradient(to right, #0dcaf0, #0aa2c0)",
                bootstrapClass: "text-bg-info"
            },
            success: {
                background: "linear-gradient(to right, #198754, #146c43)",
                bootstrapClass: "text-bg-success"
            }
        };

        const currentStyle = styles[type] || styles.danger;

        // اگر Toastify موجود است
        if (typeof Toastify !== 'undefined') {
            Toastify({
                text: message,
                duration: 3000,
                gravity: "top",
                position: "center",
                style: {
                    background: currentStyle.background,
                    borderRadius: "8px",
                    padding: "12px 20px"
                },
                close: true
            }).showToast();
            return;
        }

        // اگر Bootstrap Toast موجود است
        const toastContainer = document.getElementById('toast-container');
        if (toastContainer) {
            const toastHtml = `
            <div class="toast align-items-center ${currentStyle.bootstrapClass} border-0" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">${message}</div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
                </div>
            </div>`;
            toastContainer.insertAdjacentHTML('beforeend', toastHtml);
            const toastEl = toastContainer.lastElementChild;
            const toast = new bootstrap.Toast(toastEl, { delay: 5000 });
            toast.show();
            toastEl.addEventListener('hidden.bs.toast', () => toastEl.remove());
            return;
        }

        // Fallback به alert
        alert(message);
    }


    /**
     * محتوای یک پیام ویرایش شده را در UI به‌روز می‌کند.
     */
    function handleEditedMessage(message) {
        console.log("Received edit for messageId: " + message.messageId);
        const messageElement = $('#message-' + message.messageId);
        if (!messageElement.length) {
            console.warn("Received edit for a message that is not currently visible:", message.messageId);
            return;
        }

        const newHtml = createMessageHtmlBody(message, true);
        messageElement.replaceWith(newHtml);

        if (typeof init_iconsax === 'function') {
            init_iconsax();
        }
        console.log(`Message ${message.messageId} UI was successfully replaced and updated.`);
    }

    /**
     * وضعیت آنلاین/آفلاین یک کاربر را در UI به‌روز می‌کند
     */
    function updateUserStatusIcon(userId, isOnline, groupId = null, groupType = null) {
        const memberStatusElement = $(`#member-status-${userId}`);

        if (memberStatusElement.length > 0) {
            memberStatusElement.toggleClass('online', isOnline);

            const statusTextElement = memberStatusElement.closest('.member-details').find('.status-online');
            const newText = isOnline ? 'Online' : 'Offline';
            const newIcon = isOnline ? '/chatzy/assets/images/svg/smiling-eyes.svg'
                : '/chatzy/assets/images/svg/smile.svg';

            statusTextElement.html(`${newText} <img src="${newIcon}" alt="status-icon">`);

            console.log(`User ${userId} status updated to: ${newText}`);
        }
    }

    /**
     * لیست کاربران یک گروه را به همراه وضعیت آنلاین آنها از سرور دریافت و نمایش می‌دهد.
     */
    function loadAndDisplayOnlineUsers(groupId, groupType) {
        $.ajax({
            url: '/api/Chat/usersWithStatus',
            type: 'GET',
            data: { groupId: groupId, groupType: groupType },
            success: function (users) {
                if (users && Array.isArray(users)) {
                    users.forEach(user => {
                        updateUserStatusIcon(user.userId, user.isOnline, parseInt(groupId), groupType);
                    });
                } else {
                    console.warn("GetUsersWithStatus AJAX returned no users or invalid format for group " + groupId);
                }
            },
            error: function (xhr, status, error) {
                console.error(`Error in GetUsersWithStatus (AJAX) for group ${groupId}:`, status, error, xhr.responseText);
            }
        });
    }

    /**
     * نشانگر "در حال تایپ" را برای چندین کاربر به‌روز می‌کند.
     */
    function updateTypingIndicator(groupId) {
        const typingContainer = $(`#typing-indicator-${groupId}`);
        const currentTypers = typingUsers[groupId];

        if (!currentTypers || currentTypers.size === 0) {
            typingContainer.text('').hide();
            return;
        }

        const names = Array.from(currentTypers);
        const displayText = names.length === 1
            ? `${names[0]} در حال تایپ است...`
            : `${names.join('، ')} در حال تایپ هستند...`;

        typingContainer.text(displayText).show();
    }

    function handleUserTyping(userId, fullName, groupId) {
        if (!typingUsers[groupId]) typingUsers[groupId] = new Set();
        typingUsers[groupId].add(fullName);
        updateTypingIndicator(groupId);

        if (!typingUsers[groupId].timers) typingUsers[groupId].timers = {};
        clearTimeout(typingUsers[groupId].timers[userId]);
        typingUsers[groupId].timers[userId] = setTimeout(() => {
            handleUserStopTyping(userId, fullName, groupId);
        }, 3000);
    }

    function handleUserStopTyping(userId, fullName, groupId) {
        if (typingUsers[groupId]) {
            typingUsers[groupId].delete(fullName);
            updateTypingIndicator(groupId);
        }
    }

    /**
     * بروزرسانی وضعیت دیده شدن پیام با تعداد دیده‌ها
     */
    function handleMessageSeenUpdate(messageId, readerUserId, seenCount, readerFullName) {
        console.log('handleMessageSeenUpdate called with messageId:', messageId, 'readerUserId:', readerUserId, 'seenCount:', seenCount, 'readerFullName:', readerFullName);
        const messageElement = $('#message-' + messageId);
        if (messageElement.length && messageElement.data('sender-id') == currentUser) {
            if (seenCount > 0) {
                const timingElement = messageElement.find('.timing');
                timingElement.find('.tick').hide();
                timingElement.find('.tick-all').show();
                const currentTitle = timingElement.attr('title') || 'خوانده شده توسط:';
                if (!currentTitle.includes(readerFullName)) {
                    timingElement.attr('title', currentTitle + ' ' + readerFullName);
                }
            }
        }
    }

    /**
 * ✅ تابع کمکی: منتظر رندر شدن پیام و سپس اسکرول
 */
    function waitForElementAndScroll(messageId, maxAttempts = 10, currentAttempt = 0) {
        const targetElement = document.getElementById(`message-${messageId}`);

        if (targetElement) {
            console.log(`✅ Element found on attempt ${currentAttempt + 1}`);
            scrollToMessage(messageId);
            if (window.chatApp && window.chatApp.setScrollListenerActive) {
                window.chatApp.setScrollListenerActive(true);
            }
            return;
        }

        if (currentAttempt < maxAttempts) {
            console.log(`⏳ Waiting for element... attempt ${currentAttempt + 1}/${maxAttempts}`);
            setTimeout(() => {
                waitForElementAndScroll(messageId, maxAttempts, currentAttempt + 1);
            }, 100);
        } else {
            console.error(`❌ Element not found after ${maxAttempts} attempts`);
            if (window.chatApp && window.chatApp.setScrollListenerActive) {
                window.chatApp.setScrollListenerActive(true);
            }
        }
    }

    /**
  * ✅ تابع اسکرول دقیق به پیام (نسخه بهبودیافته)
  */
    function scrollToMessage(messageId) {
        console.log(`Scrolling to message ${messageId}...`);

        const targetElement = document.getElementById(`message-${messageId}`);
        let chatContent = document.getElementById('chat_content');

        if (!chatContent) {
            const $chatContent = $('#chat_content');
            if ($chatContent.length) {
                chatContent = $chatContent[0];
            }
        }

        if (!chatContent) {
            chatContent = document.querySelector('.chat-content, .message-container, #Message_Days') ||
                document.querySelector('div[style*="overflow"]');
        }

        if (!targetElement || !chatContent) {
            console.error(`❌ Cannot scroll: element not found. messageId: ${messageId}, chatContent: ${!!chatContent}`);
            return;
        }

        try {
            // روش ۱: استفاده از scrollIntoView
            targetElement.scrollIntoView({
                behavior: 'smooth',
                block: 'center',
                inline: 'nearest'
            });

            // روش ۲: محاسبه دستی
            setTimeout(() => {
                const elementRect = targetElement.getBoundingClientRect();
                const containerRect = chatContent.getBoundingClientRect();

                if (elementRect.top < containerRect.top || elementRect.bottom > containerRect.bottom) {
                    console.log('Using manual scroll calculation...');

                    // ✅ استفاده از getScrollTop
                    const currentScroll = window.chatUtils ?
                        window.chatUtils.getScrollTop(chatContent) :
                        chatContent.scrollTop;

                    const elementTopRelativeToContainer = elementRect.top - containerRect.top + currentScroll;
                    const containerHeight = chatContent.clientHeight;
                    const elementHeight = elementRect.height;

                    const scrollPosition = elementTopRelativeToContainer - (containerHeight / 2) + (elementHeight / 2) + 300;

                    // ✅ استفاده از setScrollTop
                    if (window.chatUtils && window.chatUtils.setScrollTop) {
                        window.chatUtils.setScrollTop(chatContent, scrollPosition);
                    } else {
                        chatContent.scrollTop = scrollPosition;
                    }
                }
            }, 100);

            // Highlight
            const $element = $(targetElement);
            $element.addClass('highlight-message');

            setTimeout(() => {
                $element.removeClass('highlight-message');
                console.log('✅ Highlight removed');
            }, 2500);

            console.log(`✅ Scroll initiated to message ${messageId}`);

        } catch (error) {
            console.error(`❌ Error during scroll:`, error);

            // Fallback: jQuery animate
            try {
                const $target = $(targetElement);
                const $container = $(chatContent);

                // ✅ استفاده از getScrollTop در jQuery fallback
                const currentScroll = window.chatUtils ?
                    window.chatUtils.getScrollTop($container) :
                    $container.scrollTop();

                $container.animate({
                    scrollTop: $target.offset().top - $container.offset().top + currentScroll - ($container.height() / 2)
                }, 500);
            } catch (fallbackError) {
                console.error('Fallback scroll also failed:', fallbackError);
            }
        }
    }

    /**
     * بروزرسانی تعداد پیام خوانده نشده
     */
    function updateUnreadCountForGroup(key, count) {
        const unreadBadge = $(`#unreadCountBadge_${key}`);
        console.log(`updateUnreadCountForGroup Called! key: ${key}, count: ${count}, type: ${typeof count}`);

        if (!unreadBadge.length) {
            console.log('unread container not found!');
            return;
        } else {
            console.log(`Current badge text: ${unreadBadge.text()}, has d-none: ${unreadBadge.hasClass('d-none')}`);
            if (count === 0) {
                console.log('Entering count === 0 block');
                unreadBadge.text(count).addClass('d-none');
            } else {
                console.log(`Entering else block with count: ${count}`);
                unreadBadge.text(count).removeClass('d-none');
            }
            console.log(`After update - badge text: ${unreadBadge.text()}, has d-none: ${unreadBadge.hasClass('d-none')}`);
        }
    }

    /**
     * وقتی پیام توسط یک فرد خوانده شد
     */
    function handleMessageSuccessfullyMarkedAsRead(messageId, groupId, groupType, unreadCount) {
        console.log(`MessageSuccessfullyMarkedAsRead called: messageId=${messageId}, groupId=${groupId}, groupType=${groupType}, unreadCount=${unreadCount}, time=${new Date().toISOString()}`);
        const messageElement = $('#message-' + messageId);
        if (messageElement.length) {
            messageElement.attr('data-is-read', 'true');
        }

        const key = `${groupType}_${groupId}`;
        updateUnreadCountForGroup(key, unreadCount)
    }

    /**
     * وقتی کاربر بر روی مشاهده همه کلیک کرد
     */
    function handleAllUnreadMessageSuccessfullyMarkedAsRead(messageIds, groupId, groupType, unreadCount) {
        console.log(`handleAllUnreadMessageSuccessfullyMarkedAsRead called: messageIds = ${messageIds}, groupId = ${groupId}, groupType = ${groupType}, unreadCount = ${unreadCount}, time = ${new Date().toISOString()}`);

        $('#chat_content .message[data-is-read="false"]').each(function () {
            $(this).attr('data-is-read', 'true');
        });

        messageIds.forEach(messageId => {
            $(`#message-${messageId}`).attr('data-is-read', 'true');
        });

        const key = `${groupType}_${groupId}`;
        updateUnreadCountForGroup(key, unreadCount)

        if (window.chatMessageManager && window.chatMessageManager.setIsMarkingAllMessagesAsRead) {
            window.chatMessageManager.setIsMarkingAllMessagesAsRead(false);
        }

        if (window.chatApp && window.chatApp.setScrollListenerActive) {
            window.chatApp.setScrollListenerActive(true);
        }

        setTimeout(() => {
            if (window.chatMessageManager && window.chatMessageManager.checkVisibleMessages) {
                window.chatMessageManager.checkVisibleMessages();
            }
        }, 100);
    }

    /**
     * مدیریت حذف پیام
     */
    function handleDeleteMessage(messageId, result) {
        console.log('indide UserDeleteMessage ' + messageId + ' and result is :' + result);
        const messageElement = $('#message-' + messageId);
        if (result === true) {
            if (messageElement.length) {
                messageElement.addClass('removing');
                setTimeout(() => {
                    messageElement.remove();
                }, 500);
            }
        } else {
            console.log('result from hub to handleDeleteMessage has error')
        }
    }

    /**
     * مدیریت پین پیام
     */
    function handlerUpdatePinMessage(messageId, messageText, isPin) {
        console.log('handlerUpdatePinMessage called with messageId:', messageId, 'isPin:', isPin);

        const placeholder = $('#pinnedMessagesPlaceholder');
        const pinnedContainer = placeholder.find('.pinned-messages-container');
        const pinnedList = placeholder.find('.pinned-messages-list');

        if (!pinnedContainer.length || !pinnedList.length) {
            console.warn('Pinned messages container not found!');
            return;
        }

        if (isPin) {
            const messageElement = $(`#message-${messageId}`);
            if (!messageElement.length) {
                console.warn(`Message element not found for messageId: ${messageId}`);
                return;
            }

            const existingItem = pinnedList.find(`.pinned-message-item[data-message-id="${messageId}"]`);
            if (existingItem.length) {
                console.log(`Message ${messageId} is already pinned.`);
                return;
            }

            const newPinnedItem = `
                <li class="pinned-message-item" data-message-id="${messageId}" style="position: relative; padding-left: 20px; margin-bottom: 0; cursor: pointer;">
                    <span class="borderPinMessage"></span>
                    <span class="pinMessageText">${messageText}</span>
                </li>
            `;

            pinnedList.append(newPinnedItem);
            console.log(`Message ${messageId} added to pinned messages.`);

            pinnedContainer.show();
            pinnedContainer.scrollTop(pinnedContainer[0].scrollHeight);

        } else {
            const pinnedItem = pinnedList.find(`.pinned-message-item[data-message-id="${messageId}"]`);
            if (pinnedItem.length) {
                pinnedItem.fadeOut(300, function () {
                    $(this).remove();

                    if (pinnedList.find('.pinned-message-item').length === 0) {
                        pinnedContainer.fadeOut(300, function () {
                            $(this).hide();
                        });
                    }
                });
                console.log(`Message ${messageId} removed from pinned messages.`);
            } else {
                console.warn(`Pinned message item not found for messageId: ${messageId}`);
            }
        }
    }

    // =================================================
    //                 PUBLIC API
    // =================================================

    return {
        init: function (userId) {
            currentUser = userId;
            console.log("UIRenderer initialized for user:", currentUser);
        },

        // متدهای اصلی
        displayMessage: displayMessage,
        updateMessageStatus: updateMessageStatus,
        updateEditMessageStatus: updateEditMessageStatus,
        handleEditedMessage: handleEditedMessage,
        updateUserStatusIcon: updateUserStatusIcon,
        loadAndDisplayOnlineUsers: loadAndDisplayOnlineUsers,
        groupMessagesByDate: groupMessagesByDate,
        scrollToMessage: scrollToMessage,
        waitForElementAndScroll: waitForElementAndScroll,
        showToast: showToast,
        // هندلرهای SignalR
        handleUserTyping: handleUserTyping,
        handleUserStopTyping: handleUserStopTyping,
        handleMessageSeenUpdate: handleMessageSeenUpdate,
        handleMessageSuccessfullyMarkedAsRead: handleMessageSuccessfullyMarkedAsRead,
        handleAllUnreadMessageSuccessfullyMarkedAsRead: handleAllUnreadMessageSuccessfullyMarkedAsRead,
        handleDeleteMessage: handleDeleteMessage,
        handlerUpdatePinMessage: handlerUpdatePinMessage,
        updateUnreadCountForGroup: updateUnreadCountForGroup,

        // توابع کمکی
        createMessageHtmlBody: createMessageHtmlBody,
        createDisplayFileBody: createDisplayFileBody,
        createMessagePreviewText: createMessagePreviewText,
        addMessageToUI: addMessageToUI,

        // توابع utility (برای backward compatibility)
        formatDate: formatDate,
        convertGregorianToJalaaliSimple: convertGregorianToJalaaliSimple,
        convertDateTohhmm: convertDateTohhmm,
        extractTime: extractTime,
        formatFileSize: formatFileSize,
        formatAudioTime: formatAudioTime,
        makeJsonObjectForMessateDetails: makeJsonObjectForMessateDetails
    };

})(jQuery);