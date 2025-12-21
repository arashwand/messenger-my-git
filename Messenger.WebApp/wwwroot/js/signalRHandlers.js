// =========================================================================
// SIGNALR HANDLERS MODULE
// =========================================================================
// ثبت و مدیریت تمام هندلرهای رویدادهای SignalR
// =========================================================================

window.chatSignalRHandlers = (function () {
    'use strict';

    // =================================================
    //                 PRIVATE METHODS
    // =================================================

    /**
     * ثبت تمام هندلرهای SignalR
     */
    function registerHandlers(connection, currentUser) {

        // دریافت پیام جدید
        connection.on("ReceiveMessage", function (message) {
            console.log("Displaying message received on handler :", message);
            if (message.senderUserId !== currentUser) {
                if (window.chatUIRenderer && window.chatUIRenderer.displayMessage) {
                    window.chatUIRenderer.displayMessage(message);
                }
            } else if (message.isSystemMessage) {
                console.log("-------------------message receive from portal-------------------");
                if (window.chatUIRenderer && window.chatUIRenderer.displayMessage) {
                    window.chatUIRenderer.displayMessage(message);
                }
            }
        });

        // دریافت پیام ویرایش شده
        connection.on("ReceiveEditedMessage", function (message) {
            if (window.chatUIRenderer && window.chatUIRenderer.handleEditedMessage) {
                window.chatUIRenderer.handleEditedMessage(message);
            }
        });

        // دریافت و اعمال تعداد پیام‌های خوانده نشده
        connection.on("UpdateUnreadCount", function (key, count) {
            console.log(`UpdateUnreadCount received: key=${key}, count=${count}, type=${typeof count}`);
            if (typeof count !== 'number' || isNaN(count)) {
                console.warn(`Invalid count value received: ${count}`);
                return;
            }
            if (window.chatUIRenderer && window.chatUIRenderer.updateUnreadCountForGroup) {
                window.chatUIRenderer.updateUnreadCountForGroup(key, count);
            }
        });

        // تغییر وضعیت آنلاین/آفلاین کاربر
        connection.on("UserStatusChanged", function (userId, isOnline, groupId, groupType) {
            console.log(`CLIENT RECEIVED: UserStatusChanged for user ${userId} in group ${groupId}. IsOnline: ${isOnline}`);
            if (window.chatUIRenderer && window.chatUIRenderer.updateUserStatusIcon) {
                window.chatUIRenderer.updateUserStatusIcon(userId, isOnline, groupId, groupType);
            }
        });

        // تایید ارسال موفق پیام
        connection.on("MessageSentSuccessfully", function (savedMessage, jsonObject) {
            console.log("Successfully sent message, server confirmation received:", savedMessage);
            if (window.chatUIRenderer && window.chatUIRenderer.updateMessageStatus) {
                window.chatUIRenderer.updateMessageStatus(savedMessage.clientMessageId, savedMessage, 'sent', jsonObject);
            }
        });

        // تایید ویرایش موفق پیام
        connection.on("EditMessageSentSuccessfully", function (savedEditMessage, jsonObject) {
            console.log("Successfully Edit message, server confirmation received:", savedEditMessage);
            if (window.chatUIRenderer && window.chatUIRenderer.updateEditMessageStatus) {
                window.chatUIRenderer.updateEditMessageStatus(savedEditMessage.messageId, savedEditMessage, 'sent', jsonObject);
            }
        });

        // خطا در ارسال پیام
        connection.on("MessageSentFailed", function (clientMessageId) {
            console.log("Edit Message Has Failed in clientMessageId:", clientMessageId);
            if (window.chatUIRenderer && window.chatUIRenderer.updateMessageStatus) {
                window.chatUIRenderer.updateMessageStatus(clientMessageId, null, 'failed');
            }
        });

        // خطا در ارسال پیام بدلیل وجود کلمات نامناسب
        connection.on("ReceiveModerationWarning", function (clientMessageId, errorMessage) {
            console.error("message has bad words !");
            // نمایش Toast یا Alert
            if (window.chatUIRenderer && window.chatUIRenderer.showToast) {
                window.chatUIRenderer.showToast(errorMessage);
            } else {
                alert(errorMessage);
            }

            // حذف پیام
            if (window.chatUIRenderer && window.chatUIRenderer.handleDeleteMessage) {
                let messageId = `msg-temp-${clientMessageId}`;
                window.chatUIRenderer.handleDeleteMessage(messageId, true);
            }

        });

        // هندلر برای خطای ویرایش پیام
        connection.on("EditMessageSentFailed", function (errorData) {
            console.error("EditMessageSentFailed received:", errorData);

            let errorMessage = 'خطا در ویرایش پیام';

            if (errorData && errorData.errorCode === 'TIME_LIMIT_EXCEEDED') {
                errorMessage = `امکان ویرایش این پیام وجود ندارد. زمان مجاز (${errorData.allowedMinutes} دقیقه) به پایان رسیده است.`;
            } else if (errorData && errorData.message) {
                errorMessage = errorData.message;
            }

            // به‌روزرسانی UI پیام
            if (window.chatUIRenderer && window.chatUIRenderer.updateEditMessageStatus) {
                window.chatUIRenderer.updateEditMessageStatus(errorData.messageId, null, 'failed', null, errorMessage);
            }

            // نمایش Toast یا Alert
            //if (window.chatUIRenderer && window.chatUIRenderer.showToast) {
            //    window.chatUIRenderer.showToast(errorMessage);
            //} else {
            //    alert(errorMessage);
            //}
        });

        // کاربر در حال تایپ
        connection.on("UserTyping", function (userId, fullName, groupId) {
            if (window.chatUIRenderer && window.chatUIRenderer.handleUserTyping) {
                window.chatUIRenderer.handleUserTyping(userId, fullName, groupId);
            }
        });

        // کاربر تایپ را متوقف کرد
        connection.on("UserStoppedTyping", function (userId, fullName, groupId) {
            if (window.chatUIRenderer && window.chatUIRenderer.handleUserStopTyping) {
                window.chatUIRenderer.handleUserStopTyping(userId, fullName, groupId);
            }
        });

        // بروزرسانی وضعیت خوانده شدن پیام
        connection.on("MessageSeenUpdate", function (messageId, readerUserId, seenCount, readerFullName) {
            if (window.chatUIRenderer && window.chatUIRenderer.handleMessageSeenUpdate) {
                window.chatUIRenderer.handleMessageSeenUpdate(messageId, readerUserId, seenCount, readerFullName);
            }
        });

        // بروزرسانی وضعیت پین پیام
        connection.on("UpdatePinMessage", function (data) {
            console.log("UpdatePinMessage received:", data);
            if (window.chatUIRenderer && window.chatUIRenderer.handlerUpdatePinMessage) {
                window.chatUIRenderer.handlerUpdatePinMessage(data.messageId, data.messageText, data.isPin);
            }
        });

        // پیام با موفقیت خوانده شد
        connection.on("MessageSuccessfullyMarkedAsRead", function (messageId, groupId, groupType, unreadCount) {
            if (window.chatUIRenderer && window.chatUIRenderer.handleMessageSuccessfullyMarkedAsRead) {
                window.chatUIRenderer.handleMessageSuccessfullyMarkedAsRead(messageId, groupId, groupType, unreadCount);
            }
        });

        // همه پیام‌های خوانده نشده با موفقیت خوانده شدند
        connection.on("AllUnreadMessagesSuccessfullyMarkedAsRead", function (messageIds, groupId, groupType, unreadCount) {
            if (window.chatUIRenderer && window.chatUIRenderer.handleAllUnreadMessageSuccessfullyMarkedAsRead) {
                window.chatUIRenderer.handleAllUnreadMessageSuccessfullyMarkedAsRead(messageIds, groupId, groupType, unreadCount);
            }

            // همچنین باید به MessageManager اطلاع دهیم
            if (window.chatMessageManager && window.chatMessageManager.setIsMarkingAllMessagesAsRead) {
                window.chatMessageManager.setIsMarkingAllMessagesAsRead(false);
            }
        });

        // حذف پیام
        connection.on("UserDeleteMessage", function (messageId, result) {
            if (window.chatUIRenderer && window.chatUIRenderer.handleDeleteMessage) {
                window.chatUIRenderer.handleDeleteMessage(messageId, result);
            }
        });

        // دریافت نتیجه پیام صوتی
        connection.on("ReceiveVoiceMessageResult", function (data) {
            console.log("ReceiveVoiceMessageResult received:", data);
            // این هندلر به کد قدیمی صدا مربوط است - می‌توانید آن را به ماژول مربوطه منتقل کنید
            handleVoiceMessageResult(data);
        });

        // مدیریت خطا در ارسال پیام
        connection.on("SendMessageError", function (errorMessage) {
            console.error("Server returned an error for sending message:", errorMessage);
            // اینجا می‌توانید یک پیام خطا به کاربر نمایش دهید
            showErrorMessage(errorMessage);
        });
    }

    /**
     * مدیریت نتیجه پیام صوتی (کد قدیمی - برای سازگاری)
     */
    function handleVoiceMessageResult(data) {
        if (data.success && data.recordingId === window.recordingId) {
            if (window.voiceUploadTimeout) {
                clearTimeout(window.voiceUploadTimeout);
                window.voiceUploadTimeout = null;
            }

            window.pendingVoiceFileId = data.fileId;

            if (window.lastRecordedBlob) {
                window.pendingVoiceUrl = URL.createObjectURL(window.lastRecordedBlob);
                window.pendingVoiceAudioElement = new Audio(window.pendingVoiceUrl);

                // این تابع باید در utils.js تعریف شود
                if (window.chatUtils && window.chatUtils.addFileIdToHiddenInput) {
                    window.chatUtils.addFileIdToHiddenInput(data.fileId, '#uploadedFileIds');
                }

                window.isProcessing = false;

                // این تابع باید در UIRenderer تعریف شود
                if (window.chatUIRenderer && window.chatUIRenderer.updateChatInputUI) {
                    window.chatUIRenderer.updateChatInputUI('preview', {
                        duration: data.duration,
                        durationFormatted: data.durationFormatted
                    });
                }
            } else {
                console.error("Last recorded blob was not found for creating a preview.");
                cleanupVoiceState();
            }
        }
    }

    /**
     * پاکسازی وضعیت صدا (کمکی)
     */
    function cleanupVoiceState() {
        if (window.pendingVoiceAudioElement) {
            window.pendingVoiceAudioElement.pause();
            window.pendingVoiceAudioElement = null;
        }
        if (window.pendingVoiceUrl) {
            URL.revokeObjectURL(window.pendingVoiceUrl);
            window.pendingVoiceUrl = null;
        }
        window.pendingVoiceFileId = null;
        window.isProcessing = false;
    }

    /**
     * نمایش خطا به کاربر
     */
    function showErrorMessage(message) {
        // می‌توانید از toast یا modal استفاده کنید
        console.error("Error message to display:", message);

        // مثال ساده با alert (در پروژه واقعی بهتر است از روش‌های زیباتر استفاده کنید)
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                icon: 'error',
                title: 'خطا',
                text: message,
                timer: 3000
            });
        } else {
            alert("خطا: " + message);
        }
    }

    // =================================================
    //                 PUBLIC API
    // =================================================

    return {
        /**
         * ثبت تمام هندلرهای SignalR
         * @param {signalR.HubConnection} connection - اتصال SignalR
         * @param {number} currentUser - شناسه کاربر جاری
         */
        registerHandlers: registerHandlers,

        /**
         * دریافت هندلر خاص (در صورت نیاز)
         */
        getHandler: function (handlerName) {
            const handlers = {
                ReceiveMessage: function (message) {
                    if (window.chatUIRenderer && window.chatUIRenderer.displayMessage) {
                        window.chatUIRenderer.displayMessage(message);
                    }
                },
                ReceiveEditedMessage: function (message) {
                    if (window.chatUIRenderer && window.chatUIRenderer.handleEditedMessage) {
                        window.chatUIRenderer.handleEditedMessage(message);
                    }
                },
                // ... سایر هندلرها
            };

            return handlers[handlerName] || null;
        },

        /**
         * حذف همه هندلرها (برای cleanup)
         */
        removeAllHandlers: function (connection) {
            if (!connection) return;

            const handlerNames = [
                "ReceiveMessage",
                "ReceiveEditedMessage",
                "UpdateUnreadCount",
                "UserStatusChanged",
                "MessageSentSuccessfully",
                "EditMessageSentSuccessfully",
                "MessageSentFailed",
                "EditMessageSentFailed",
                "UserTyping",
                "UserStoppedTyping",
                "MessageSeenUpdate",
                "UpdatePinMessage",
                "MessageSuccessfullyMarkedAsRead",
                "AllUnreadMessagesSuccessfullyMarkedAsRead",
                "UserDeleteMessage",
                "ReceiveVoiceMessageResult",
                "SendMessageError"
            ];

            handlerNames.forEach(handlerName => {
                connection.off(handlerName);
            });

            console.log("All SignalR handlers removed.");
        }
    };

})();