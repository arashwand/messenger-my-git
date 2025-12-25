// =========================================================================
// MESSAGE MANAGER MODULE
// =========================================================================
// مدیریت پیام‌ها، بارگذاری تاریخچه، اسکرول و وضعیت پیام‌ها
// =========================================================================

window.chatMessageManager = (function ($) {
    'use strict';

    // =================================================
    //            PRIVATE VARIABLES & PROPERTIES
    // =================================================
    let isScrollRequestRunning = false;
    let hasReachedOldestMessage = false;
    let isMarkingAllMessagesAsRead = false;
    let getOldDataRunning = false;
    let getNewerDataRunning = false;
    let isLoadingAroundMessage = false;
    let isInitialLoad = true;
    let scrollTimer = null;

    // مدیریت وضعیت پیام‌های بارگذاری شده برای پشتیبانی از "خلا"
    let messageRanges = []; // آرایه‌ای از بازه‌ها { oldestId: number, newestId: number }

    // =================================================
    //                 PRIVATE METHODS
    // =================================================

    /**
     * وضعیت پیام‌ها را برای یک چت جدید ریست می‌کند.
     */
    function resetMessageState() {
        console.log("Resetting message state.");
        messageRanges = [];
        hasReachedOldestMessage = false;
        $('#lastMessageIdLoad').val(0);
    }

    /**
     * نشانگرهای بصری برای "خلا" بین بازه‌های پیام را در UI مدیریت می‌کند.
     */
    function updateGapIndicators() {
        $('.gap-indicator').remove();

        if (messageRanges.length < 2) {
            return;
        }

        console.log("Updating gap indicators...");

        for (let i = 0; i < messageRanges.length - 1; i++) {
            const currentRange = messageRanges[i];
            const nextRange = messageRanges[i + 1];

            if (currentRange.newestId + 1 !== nextRange.oldestId) {
                const lastMessageOfRange = $(`#message-${currentRange.newestId}`);
                if (lastMessageOfRange.length) {
                    const parentList = lastMessageOfRange.closest('ul.message-box-list');
                    const indicatorHtml = `
                        <div class="gap-indicator text-center p-3 my-2 text-muted fst-italic">
                            ... پیام‌های بیشتر ...
                        </div>`;
                    parentList.after(indicatorHtml);
                }
            }
        }
    }

    /**
     * بازه‌های پیام را بر اساس پیام‌های جدید به‌روز می‌کند.
     */
    function updateMessageRanges(messages) {
        if (!messages || messages.length === 0) return;

        const ids = messages.map(m => m.messageId);
        const newOldestId = Math.min(...ids);
        const newNewestId = Math.max(...ids);

        console.log(`Updating ranges with new messages: ${newOldestId} -> ${newNewestId}`);

        let overlappingRanges = [];
        let otherRanges = [];

        for (const range of messageRanges) {
            if ((range.oldestId <= newNewestId + 1) && (range.newestId + 1 >= newOldestId)) {
                overlappingRanges.push(range);
            } else {
                otherRanges.push(range);
            }
        }

        if (overlappingRanges.length === 0) {
            otherRanges.push({ oldestId: newOldestId, newestId: newNewestId });
            messageRanges = otherRanges;
        } else {
            const allOverlappingIds = overlappingRanges.flatMap(r => [r.oldestId, r.newestId]);
            const mergedOldestId = Math.min(newOldestId, ...allOverlappingIds);
            const mergedNewestId = Math.max(newNewestId, ...allOverlappingIds);

            otherRanges.push({ oldestId: mergedOldestId, newestId: mergedNewestId });
            messageRanges = otherRanges;
        }

        messageRanges.sort((a, b) => a.oldestId - b.oldestId);

        if (messageRanges.length > 0) {
            $('#lastMessageIdLoad').val(messageRanges[0].oldestId);
        }

        console.log("Updated message ranges:", JSON.stringify(messageRanges));
        updateGapIndicators();
    }

    /**
     * پیام‌های جدیدتر از یک شناسه مشخص را بارگذاری می‌کند (برای پر کردن خلا).
     */
    function getNewerData(startMessageId) {
        if (getNewerDataRunning || isScrollRequestRunning) {
            console.log("getNewerData is already running or scroll request is active.");
            return Promise.resolve();
        }

        console.log(`Fetching newer messages starting after ID: ${startMessageId}`);
        getNewerDataRunning = true;

        const chatId = parseInt($('#current-group-id-hidden-input').val());
        const currentGroupType = $('#current-group-type-hidden-input').val();
        const chatKey = $('#chatKey').val();

        // تعیین URL و پارامترها بر اساس نوع چت
        let ajaxConfig;
        if (currentGroupType === 'Private') {
            if (!chatKey) {
                console.error('chatKey not found for private chat');
                getNewerDataRunning = false;
                return Promise.resolve();
            }
            ajaxConfig = {
                url: '/Home/GetNewerPrivateChatMessages',
                type: 'GET',
                data: {
                    chatKey: chatKey,
                    messageId: startMessageId,
                    pageSize: 50
                }
            };
        } else {
            ajaxConfig = {
                url: '/Home/GetNewerMessages',
                type: 'GET',
                data: {
                    chatId: chatId,
                    groupType: currentGroupType,
                    messageId: startMessageId,
                    pageSize: 50
                }
            };
        }

        return $.ajax({
            ...ajaxConfig,
            success: function (response) {
                if (response.success && response.data.length > 0) {
                    console.log(`✅ Loaded ${response.data.length} newer messages.`);
                    updateMessageRanges(response.data);

                    // فراخوانی تابع گروه‌بندی از ماژول UI
                    if (window.chatUIRenderer && window.chatUIRenderer.groupMessagesByDate) {
                        window.chatUIRenderer.groupMessagesByDate(response.data, false);
                    }

                    // پیش‌بارگذاری صدا
                    if (window.preloadCachedFor) {
                        window.preloadCachedFor(document.getElementById('Message_Days'));
                    }
                } else {
                    console.log("No newer messages found or error occurred.");
                }
            },
            complete: function () {
                getNewerDataRunning = false;
            },
            error: function (xhr, status, error) {
                console.error("Error fetching newer messages:", error);
            }
        });
    }

    /**
     * دریافت اطلاعات قدیمی تر جهت نمایش به کاربر
     */
    function getOldData(targetMessageId = null, loadBothDirections = false) {
        console.log('isScrollRequestRunning: ' + isScrollRequestRunning);
        console.log('hasReachedOldestMessage : ' + hasReachedOldestMessage + ' and getOldDataRunning is: ' + getOldDataRunning);
        console.log('targetMessageId: ' + targetMessageId + ', loadBothDirections: ' + loadBothDirections);

        var lastmessageId = $('#lastMessageIdLoad').val();
        if (lastmessageId == 0) {
            console.log("No messages loaded yet, skipping getOldData.");
            return Promise.resolve();
        }

        if (getOldDataRunning || isScrollRequestRunning) {
            console.log('getOldData is already running or scroll request is active');
            return Promise.resolve();
        }

        // اگر targetMessageId داریم، حول آن بارگذاری کن
        if (targetMessageId && loadBothDirections) {
            if (isLoadingAroundMessage) {
                console.log('Already loading around a target message');
                return Promise.resolve();
            }

            const existingElement = document.getElementById(`message-${targetMessageId}`);
            if (existingElement) {
                console.log(`✅ Message ${targetMessageId} already exists in DOM. Scrolling to it.`);
                if (window.chatUIRenderer && window.chatUIRenderer.scrollToMessage) {
                    window.chatUIRenderer.scrollToMessage(targetMessageId);
                }
                return;
            }

            isLoadingAroundMessage = true;
            getOldDataRunning = true;

            resetMessageState();

            const chatId = parseInt($('#current-group-id-hidden-input').val());
            const currentGroupType = $('#current-group-type-hidden-input').val();

            console.log(`Loading messages around message ID: ${targetMessageId}`);

            // غیرفعال کردن لیسنر اسکرول موقتاً
            if (window.chatApp && window.chatApp.setScrollListenerActive) {
                window.chatApp.setScrollListenerActive(false);
            }

            return $.ajax({
                url: '/Home/GetOldMessage',
                type: 'POST',
                data: {
                    chatId: chatId,
                    groupType: currentGroupType,
                    messageId: targetMessageId,
                    loadOlder: false,
                    loadBothDirections: true
                },
                success: function (response) {
                    if (response.success && response.data.length > 0) {
                        console.log(`✅ Loaded ${response.data.length} messages around target message`);

                        updateMessageRanges(response.data);

                        if (window.chatUIRenderer && window.chatUIRenderer.groupMessagesByDate) {
                            window.chatUIRenderer.groupMessagesByDate(response.data);
                        }

                        if (window.preloadCachedFor) {
                            window.preloadCachedFor(document.getElementById('Message_Days'));
                        }

                        if (window.chatUIRenderer && window.chatUIRenderer.waitForElementAndScroll) {
                            window.chatUIRenderer.waitForElementAndScroll(targetMessageId);
                        }
                    } else {
                        console.warn('❌ No messages found around target message');
                        if (window.chatApp && window.chatApp.setScrollListenerActive) {
                            window.chatApp.setScrollListenerActive(true);
                        }
                    }
                },
                complete: function () {
                    getOldDataRunning = false;
                    isLoadingAroundMessage = false;
                },
                error: function (xhr, status, error) {
                    console.error('❌ Error loading messages around target:', error);
                    isLoadingAroundMessage = false;
                    if (window.chatApp && window.chatApp.setScrollListenerActive) {
                        window.chatApp.setScrollListenerActive(true);
                    }
                }
            });
        }

        // ---- بقیه کد برای بارگذاری قدیمی‌تر ----
        if (hasReachedOldestMessage) {
            console.log('No more messages to load');
            return Promise.resolve();
        }

        getOldDataRunning = true;
        var lastmessageId = $('#lastMessageIdLoad').val();

        if (lastmessageId == 0) {
            console.log("lastMessageId is 0, stopping.");
            getOldDataRunning = false;
            return;
        }

        console.log('last messageId is :' + lastmessageId);
        const chatId = parseInt($('#current-group-id-hidden-input').val());
        const currentGroupType = $('#current-group-type-hidden-input').val();
        const chatKey = $('#chatKey').val();

        // تعیین URL و پارامترها بر اساس نوع چت
        let ajaxConfig;
        if (currentGroupType === 'Private') {
            if (!chatKey) {
                console.error('chatKey not found for private chat');
                getOldDataRunning = false;
                return Promise.resolve();
            }
            ajaxConfig = {
                url: '/Home/GetOlderPrivateChatMessages',
                type: 'GET',
                data: {
                    chatKey: chatKey,
                    messageId: lastmessageId,
                    pageSize: 50
                }
            };
        } else {
            ajaxConfig = {
                url: '/Home/GetOldMessage',
                type: 'POST',
                data: {
                    chatId: chatId,
                    groupType: currentGroupType,
                    messageId: lastmessageId,
                    loadOlder: true,
                    loadBothDirections: false
                }
            };
        }

        return $.ajax({
            ...ajaxConfig,
            success: function (response) {
                if (response.success) {
                    if (response.data.length < 50) {
                        hasReachedOldestMessage = true;
                    }

                    if (response.data.length > 0) {
                        updateMessageRanges(response.data);

                        if (window.chatUIRenderer && window.chatUIRenderer.groupMessagesByDate) {
                            window.chatUIRenderer.groupMessagesByDate(response.data);
                        }

                        if (window.preloadCachedFor) {
                            window.preloadCachedFor(document.getElementById('Message_Days'));
                        }
                    }

                    console.log('✅ Older messages loaded successfully!');
                } else {
                    console.error('❌ Error loading old messages: ' + response.message);
                }
            },
            complete: function () {
                getOldDataRunning = false;
            },
            error: function () {
                console.error('❌ Network error while loading messages.');
            }
        });
    }

    /**
     * به یک پیام خاص در تاریخچه چت پرش می‌کند.
     */
    function jumpToMessage(targetMessageId) {
        console.log(`Jumping to message ID: ${targetMessageId}`);

        const existingElement = document.getElementById(`message-${targetMessageId}`);
        if (existingElement) {
            console.log(`✅ Message ${targetMessageId} already in DOM. Scrolling.`);
            if (window.chatUIRenderer && window.chatUIRenderer.scrollToMessage) {
                window.chatUIRenderer.scrollToMessage(targetMessageId);
            }
            return;
        }

        console.log(`Message ${targetMessageId} not found in DOM. Fetching from server.`);

        if (window.chatApp && window.chatApp.setScrollListenerActive) {
            window.chatApp.setScrollListenerActive(false);
        }

        const chatId = parseInt($('#current-group-id-hidden-input').val());
        const currentGroupType = $('#current-group-type-hidden-input').val();

        return $.ajax({
            url: '/Home/GetMessagesAroundTarget',
            type: 'GET',
            data: {
                chatId: chatId,
                groupType: currentGroupType,
                targetMessageId: targetMessageId
            },
            success: function (response) {
                if (response.success && response.data.length > 0) {
                    console.log(`✅ Loaded ${response.data.length} messages around target message`);
                    updateMessageRanges(response.data);

                    if (window.chatUIRenderer && window.chatUIRenderer.groupMessagesByDate) {
                        window.chatUIRenderer.groupMessagesByDate(response.data);
                    }

                    if (window.preloadCachedFor) {
                        window.preloadCachedFor(document.getElementById('Message_Days'));
                    }

                    if (window.chatUIRenderer && window.chatUIRenderer.waitForElementAndScroll) {
                        window.chatUIRenderer.waitForElementAndScroll(targetMessageId);
                    }
                } else {
                    console.warn('❌ No messages found around target message');
                    if (window.chatApp && window.chatApp.setScrollListenerActive) {
                        window.chatApp.setScrollListenerActive(true);
                    }
                }
            },
            complete: function () {
                if (window.chatApp && window.chatApp.setScrollListenerActive) {
                    window.chatApp.setScrollListenerActive(true);
                }
            },
            error: function (xhr, status, error) {
                console.error('❌ Error loading messages around target:', error);
                if (window.chatApp && window.chatApp.setScrollListenerActive) {
                    window.chatApp.setScrollListenerActive(true);
                }
            }
        });
    }

    /**
     * پیام‌های پین‌شده را برای یک چت خاص بارگذاری و نمایش می‌دهد.
     */
    function loadPinnedMessages(chatId, groupType) {
        console.log(`Loading pinned messages for chat ${chatId} (${groupType})`);
        const placeholder = $('#pinnedMessagesPlaceholder');

        if (!placeholder.length) {
            console.error("Pinned messages placeholder (#pinnedMessagesPlaceholder) not found.");
            return;
        }

        return $.ajax({
            url: '/Home/GetChatPinnedMessages',
            type: 'GET',
            data: {
                chatId: chatId,
                groupType: groupType
            },
            success: function (responseHtml) {
                placeholder.html(responseHtml);

                if (placeholder.find('.pinned-message-item').length > 0) {
                    placeholder.show();
                } else {
                    placeholder.hide();
                }
            },
            error: function (xhr, status, error) {
                console.error('Error loading pinned messages:', error);
                placeholder.hide();
            }
        });
    }

    /**
     * بررسی پیام‌های قابل مشاهده برای علامت‌گذاری به عنوان خوانده شده
     */
    function checkVisibleMessages(specificMessageElement = null) {
        if (isMarkingAllMessagesAsRead) {
            console.log("Skipping checkVisibleMessages because MarkAllMessagesAsRead is in progress.");
            return;
        }

        const currentGroupIdForCheck = parseInt($('#current-group-id-hidden-input').val());
        const currentGroupTypeForCheck = $('#current-group-type-hidden-input').val();
        const chatContent = $('#chat_content');

        const currentUser = window.chatApp && window.chatApp.connection ?
            parseInt($('#userId').val()) : null;

        if (!currentUser || !chatContent.length || !(currentGroupIdForCheck > 0)) {
            return;
        }

        const signalRConnection = window.chatApp ? window.chatApp.connection : null;
        if (!signalRConnection || signalRConnection.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        const processSingleMessage = (msgElement) => {
            const messageId = msgElement.data('message-id');
            const senderId = msgElement.data('sender-id');

            if (senderId === currentUser || typeof senderId === 'undefined') {
                if (senderId === currentUser) {
                    msgElement.attr('data-is-read', 'true');
                }
                return;
            }

            const chatScrollTop = chatContent.scrollTop();
            const chatHeight = chatContent.innerHeight();
            const messageVisibleTop = msgElement.offset().top - chatContent.offset().top + chatScrollTop;
            const messageVisibleBottom = messageVisibleTop + msgElement.outerHeight();
            const viewportTop = chatScrollTop;
            const viewportBottom = chatScrollTop + chatHeight;

            if (messageVisibleBottom > viewportTop && messageVisibleTop < viewportBottom && messageId) {
                msgElement.attr('data-is-read', 'true');
                if (window.chatApp && window.chatApp.markMessageAsRead) {
                    window.chatApp.markMessageAsRead(currentGroupIdForCheck, currentGroupTypeForCheck, messageId);
                }
            }
        };

        if (specificMessageElement && specificMessageElement.length && specificMessageElement.attr('data-is-read') !== 'true') {
            processSingleMessage(specificMessageElement);
        } else if (!specificMessageElement) {
            const unreadMessages = chatContent.find('.message:not([data-is-read="true"])').filter(function () {
                return $(this).data('sender-id') !== currentUser && typeof $(this).data('sender-id') !== 'undefined';
            });

            console.log(`Processing ${unreadMessages.length} unread messages from other users.`);
            unreadMessages.each(function () {
                processSingleMessage($(this));
            });
        }
    }

    function setScrollListenerActive(active) {
        const chatContentScroller = $('#chat_content');
        const newMessagesNotice = $('#newMessagesNotice');
        const arrowDown = $('.svg-arrow-down');

        chatContentScroller.off('scroll.chatApp');
        clearTimeout(scrollTimer);

        if (active) {
            console.log("Scroll listener activated.");

            chatContentScroller.on('scroll.chatApp', function () {
                const scroller = $(this);
                const scrollTop = scroller.scrollTop();
                const scrollHeight = scroller.prop("scrollHeight");
                const clientHeight = scroller.innerHeight();

                const distanceFromBottom = scrollHeight - (scrollTop + clientHeight);

                // =========================
                // نمایش / مخفی کردن فلش پایین
                // =========================
                if (distanceFromBottom > 200) {
                    if (!arrowDown.is(':visible')) {
                        arrowDown.fadeIn(150);
                    }
                } else {
                    if (arrowDown.is(':visible')) {
                        arrowDown.fadeOut(150);
                    }
                }

                // --- منطق اسکرول به بالا ---
                if (scrollTop <= 200 && !isScrollRequestRunning) {
                    isScrollRequestRunning = true;
                    getOldData().finally(() => {
                        isScrollRequestRunning = false;
                    });
                }

                // --- منطق اسکرول به پایین (پر کردن خلا) ---
                if (distanceFromBottom <= 200 && !isScrollRequestRunning) {
                    const lastMessageInDom = $('#Message_Days .message').last();
                    if (lastMessageInDom.length) {
                        const lastMessageIdInDom = lastMessageInDom.data('message-id');
                        const currentRange = messageRanges.find(r =>
                            lastMessageIdInDom >= r.oldestId &&
                            lastMessageIdInDom <= r.newestId
                        );

                        if (currentRange) {
                            const rangeIndex = messageRanges.findIndex(r =>
                                r.oldestId === currentRange.oldestId
                            );

                            if (rangeIndex < messageRanges.length - 1) {
                                console.log(`Gap detected below. Loading newer messages after ${currentRange.newestId}`);
                                isScrollRequestRunning = true;
                                getNewerData(currentRange.newestId).finally(() => {
                                    isScrollRequestRunning = false;
                                });
                            }
                        }
                    }

                    // مخفی کردن اعلان پیام جدید
                    if (newMessagesNotice.is(':visible')) {
                        newMessagesNotice.hide().data('newCount', 0).text('');
                    }
                }

                clearTimeout(scrollTimer);
                scrollTimer = setTimeout(function () {
                    checkVisibleMessages();
                }, 250);
            });
        } else {
            console.log("Scroll listener deactivated.");
        }
    }

    


    function userDeleteMessage(groupId, groupType, messageId) {
        const payload = {
            groupId: parseInt(groupId),
            groupType: groupType,
            messageId: parseInt(messageId)
        };

        return $.ajax({
            url: '/api/Chat/deleteMessage',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload),
            success: function (response) {
                if (response.success) {
                    console.log(`Message ${messageId} deleted successfully`);
                }
            },
            error: function (xhr, status, error) {
                let errorMessage = 'خطا در حذف پیام';

                if (xhr.responseJSON) {
                    const resp = xhr.responseJSON;

                    if (resp.errorCode === 'TIME_LIMIT_EXCEEDED') {
                        errorMessage = `امکان حذف این پیام وجود ندارد. زمان مجاز (${resp.allowedMinutes} دقیقه) به پایان رسیده است.`;
                    } else if (resp.message) {
                        errorMessage = resp.message;
                    }
                }

                // نمایش پیام خطا به کاربر
                if (window.chatUIRenderer && window.chatUIRenderer.showToast) {
                    window.chatUIRenderer.showToast(errorMessage,'info');
                } else {
                    alert(errorMessage);
                }

                console.error(`Error deleting message ${messageId}:`, errorMessage);
            }
        });
    }

    // =================================================
    //                 PUBLIC API
    // =================================================

    return {
        init: function () {
            console.log("MessageManager initialized");
        },

        // متدهای اصلی
        jumpToMessage: jumpToMessage,
        loadPinnedMessages: loadPinnedMessages,
        getOldData: getOldData,
        checkVisibleMessages: checkVisibleMessages,
        setScrollListenerActive: setScrollListenerActive,
        userDeleteMessage: userDeleteMessage,

        // متدهای کمکی
        reAttachScrollListener: function () {
            setScrollListenerActive(true);
        },

        resetMessageState: resetMessageState,
        updateMessageRanges: updateMessageRanges,

        // دسترسی به متغیرهای داخلی 
        getMessageRanges: function () { return messageRanges; },
        getHasReachedOldestMessage: function () { return hasReachedOldestMessage; },
        setIsMarkingAllMessagesAsRead: function (value) { isMarkingAllMessagesAsRead = value; }
    };

})(jQuery);