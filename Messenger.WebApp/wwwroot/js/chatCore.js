// =========================================================================
// CHAT CORE MODULE (chatApp) - Main Entry Point
// =========================================================================
// این ماژول اصلی، API عمومی را ارائه می‌دهد و ماژول‌های دیگر را مدیریت می‌کند.
// =========================================================================

window.chatApp = (function ($) {
    'use strict';

    // =================================================
    //            PRIVATE VARIABLES & PROPERTIES
    // =================================================
    let signalRConnection = null;
    let currentUser = null;
    let heartbeatTimer = null;
    const HEARTBEAT_INTERVAL = 180 * 1000;

    // ارجاع به ماژول‌های دیگر
    let messageManager = window.chatMessageManager || {};
    let uiRenderer = window.chatUIRenderer || {};
    let signalRHandlers = window.chatSignalRHandlers || {};
    let chatUtils = window.chatUtils || {};

    // =================================================
    //                 PRIVATE METHODS
    // =================================================

    // تابع برای ارسال Heartbeat
    function sendHeartbeatSignal() {
        if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
            console.log("Sending Heartbeat signal...");
            signalRConnection.invoke("SendHeartbeat")
                .catch(err => console.error("Error sending heartbeat signal: ", err));
        } else {
            console.warn("SignalR connection not active for heartbeat.");
        }
    }

    //تابع اعلام وضعیت کاربر آنلاین شده
    async function announceUserPresence() {
        console.log("Announcing user presence to the main API...");
        try {
            const response = await fetch('/api/chat/announce', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (response.ok) {
                const result = await response.json();
                console.log("Presence announced successfully:", result.message);
            } else {
                console.error("Failed to announce presence. Status:", response.status);
            }
        } catch (error) {
            console.error("A network error occurred while announcing presence:", error);
        }
    }

    // =================================================
    //                 PUBLIC API
    // =================================================

    const publicApi = {
        connection: null,

        // متدهای اصلی که از ماژول‌های دیگر می‌آیند
        displayMessage: function (message) {
            return uiRenderer.displayMessage ? uiRenderer.displayMessage(message) : console.warn('UIRenderer not loaded');
        },

        jumpToMessage: function (targetMessageId) {
            return messageManager.jumpToMessage ? messageManager.jumpToMessage(targetMessageId) : console.warn('MessageManager not loaded');
        },

        loadPinnedMessages: function (chatId, groupType) {
            return messageManager.loadPinnedMessages ? messageManager.loadPinnedMessages(chatId, groupType) : console.warn('MessageManager not loaded');
        },

        /**
         * ماژول چت را راه‌اندازی کرده و به SignalR متصل می‌شود.
         */
        init: function () {
            // فراخوانی اولیه برای بارگذاری پسوندها
            publicApi.callAlloewExtentions();

            currentUser = $('#userId').val();
            if (!currentUser) {
                console.error("UserId not found. ChatApp cannot initialize.");
                return;
            }
            currentUser = parseInt(currentUser);

            signalRConnection = new signalR.HubConnectionBuilder()
                .withUrl("/webappchathub")
                .withAutomaticReconnect()
                .build();

            publicApi.connection = signalRConnection;

            // ثبت هندلرهای SignalR
            if (signalRHandlers.registerHandlers) {
                signalRHandlers.registerHandlers(signalRConnection, currentUser);
            } else {
                console.warn('SignalRHandlers not loaded');
            }

            // مقداردهی اولیه ماژول‌ها
            if (messageManager.init) messageManager.init();
            if (uiRenderer.init) uiRenderer.init(currentUser);

            signalRConnection.start()
                .then(() => {
                    console.log("ChatApp initialized and connected for user: " + currentUser);

                    // اعلام حضور کاربر
                    announceUserPresence();

                    // درخواست تعداد پیام‌های خوانده نشده
                    console.log("Requesting unread counts from server...");
                    signalRConnection.invoke("RequestUnreadCounts")
                        .catch(err => console.error("Error requesting unread counts: ", err));

                    // فعال کردن لیسنر اسکرول
                    publicApi.setScrollListenerActive(true);

                    // راه‌اندازی تایمر Heartbeat
                    if (heartbeatTimer) clearInterval(heartbeatTimer);
                    heartbeatTimer = setInterval(sendHeartbeatSignal, HEARTBEAT_INTERVAL);
                    console.log(`Heartbeat timer started, sending every ${HEARTBEAT_INTERVAL / 1000} seconds.`);

                })
                .catch(err => console.error("SignalR Connection error:", err));

            signalRConnection.onclose((error) => {
                console.warn("SignalR connection closed.", error);
                if (heartbeatTimer) {
                    clearInterval(heartbeatTimer);
                    heartbeatTimer = null;
                    console.log("Heartbeat timer stopped due to connection close.");
                }
            });
        },

        disconnect: function () {
            if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
                console.log("Attempting to disconnect from SignalR hub...");

                if (heartbeatTimer) {
                    clearInterval(heartbeatTimer);
                    heartbeatTimer = null;
                    console.log("Heartbeat timer stopped due to manual disconnect.");
                }

                return signalRConnection.stop();
            }
            return Promise.resolve();
        },

        /**
         * لیست کاربران یک گروه را به همراه وضعیت آنلاین آنها از سرور دریافت و نمایش می‌دهد.
         */
        loadAndDisplayOnlineUsers: function (groupId, groupType) {
            if (uiRenderer.loadAndDisplayOnlineUsers) {
                uiRenderer.loadAndDisplayOnlineUsers(groupId, groupType);
            }
        },

        /**
         * یک پیام متنی به گروه مشخص شده ارسال می‌کند
         */
        sendMessage: function (groupId, messageText, groupType, replyToMessageId, fileAttachementIds, clientMessageId) {
            if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
                const request = {
                    GroupId: parseInt(groupId),
                    MessageText: messageText,
                    GroupType: groupType,
                    ReplyToMessageId: replyToMessageId ? parseInt(replyToMessageId) : null,
                    FileAttachementIds: fileAttachementIds && fileAttachementIds.length > 0 ? fileAttachementIds.map(id => parseInt(id)) : [],
                    ClientMessageId: clientMessageId
                };

                signalRConnection.invoke("SendMessage", request)
                    .catch(err => {
                        console.error("Error sending message via Hub:", err);
                        if (uiRenderer.updateMessageStatus) {
                            uiRenderer.updateMessageStatus(clientMessageId, null, 'failed');
                        }
                    });
            } else {
                console.error("SignalR connection not established.");
            }
        },

        /**
         * یک پیام موجود را ویرایش می‌کند.
         */
        editMessage: function (messageId, newText, groupId, groupType, fileIds, fileIdsToRemove) {
            console.log('messageId, newText, groupId, groupType : ' + messageId + ' - ' + newText + ' - ' + groupId + ' - ' + groupType);
            if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
                const request = {
                    messageId: messageId,
                    messageText: newText,
                    groupId: parseInt(groupId),
                    groupType: groupType,
                    fileAttachementIds: fileIds && fileIds.length > 0 ? fileIds.map(id => parseInt(id)) : [],
                    fileIdsToRemove: fileIdsToRemove && fileIdsToRemove.length > 0 ? fileIdsToRemove.map(id => parseInt(id)) : []
                };

                signalRConnection.invoke("EditMessage", request)
                    .catch(err => {
                        console.error("Error editing message via Hub:", err);
                        if (uiRenderer.updateEditMessageStatus) {
                            uiRenderer.updateEditMessageStatus(messageId, null, 'failed');
                        }
                    });
            } else {
                console.error("SignalR connection not established.");
            }
        },

        /**
         * وضعیت "در حال تایپ" را به سرور اطلاع می‌دهد.
         */
        sendTyping: function (groupId, groupType) {
            if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
                signalRConnection.invoke("SendTypingSignal", parseInt(groupId), groupType)
                    .catch(err => console.error("Error sending typing signal: ", err));
            }
        },

        /**
         * وضعیت "توقف تایپ" را به سرور اطلاع می‌دهد.
         */
        stopTyping: function (groupId, groupType) {
            if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
                signalRConnection.invoke("SendStopTypingSignal", parseInt(groupId), groupType)
                    .catch(err => console.error("Error sending stop typing signal: ", err));
            }
        },

        /**
         * Marks a message as read by the current user.
         */
        markMessageAsRead: function (groupId, groupType, messageId) {
            if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
                signalRConnection.invoke("MarkMessageAsRead", parseInt(groupId), groupType, parseInt(messageId))
                    .catch(err => console.error("Error marking message as read: ", err));
            }
        },

        /**
         * Marks all message in this group or channel as read by the current user.
         */
        markMarkAllMessagesAsRead: function (groupId, groupType) {
            if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
                signalRConnection.invoke("MarkAllMessagesAsRead", parseInt(groupId), groupType)
                    .catch(err => console.error("Error marking message as read: ", err));
            }
        },

        setScrollListenerActive: function (active) {
            if (messageManager.setScrollListenerActive) {
                messageManager.setScrollListenerActive(active);
            }
        },

        reAttachScrollListener: function () {
            if (messageManager.reAttachScrollListener) {
                messageManager.reAttachScrollListener();
            }
        },

        triggerGetoldData: function (messageId, loadBothDirections) {
            if (messageManager.getOldData) {
                messageManager.getOldData(messageId, loadBothDirections);
            }
        },

        userDeleteMessage: function (groupId, groupType, messageId) {
            if (messageManager.userDeleteMessage) {
                messageManager.userDeleteMessage(groupId, groupType, messageId);
            }
        },

        triggerVisibilityCheck: function () {
            if (messageManager.checkVisibleMessages) {
                console.log("Public API: Manually triggering checkVisibleMessages.");
                messageManager.checkVisibleMessages();
            }
        },

        // Properties for allowed file extensions
        ALLOWED_IMAGES: [],
        ALLOWED_DOCS: [],
        ALLOWED_AUDIO: [],

        callAlloewExtentions: async function loadFileExtensions() {
            try {
                const response = await fetch('/Home/GetAllowedExtensions');
                if (!response.ok) {
                    throw new Error('Failed to fetch allowed extensions');
                }
                const data = await response.json();

                publicApi.ALLOWED_IMAGES = data.allowedImages || [];
                publicApi.ALLOWED_DOCS = data.allowedDocs || [];
                publicApi.ALLOWED_AUDIO = data.allowedAudio || [];
                console.log("Allowed extensions loaded and set publicly.");

            } catch (error) {
                console.error('Error loading extensions:', error);
            }
        },

        // دسترسی مستقیم به ماژول‌ها (اختیاری)
        getMessageManager: function () { return messageManager; },
        getUIRenderer: function () { return uiRenderer; },
        getSignalRHandlers: function () { return signalRHandlers; },
        getUtils: function () { return chatUtils; }
    };

    return publicApi;

})(jQuery);