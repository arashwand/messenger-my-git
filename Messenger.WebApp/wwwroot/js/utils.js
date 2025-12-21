// =========================================================================
// UTILITIES MODULE
// =========================================================================
// توابع کمکی و ابزارهای عمومی
// =========================================================================

window.chatUtils = (function () {
    'use strict';

    // =================================================
    //                 PRIVATE METHODS
    // =================================================

    /**
     * تبدیل تاریخ میلادی به شمسی ساده
     */
    function convertGregorianToJalaaliSimple(dateStr) {
        // این یک پیاده‌سازی ساده است
        // برای پیاده‌سازی کامل از کتابخانه‌هایی مثل jalali-moment استفاده کنید
        try {
            const parts = dateStr.split('-');
            if (parts.length === 3) {
                const year = parseInt(parts[0]);
                const month = parseInt(parts[1]);
                const day = parseInt(parts[2]);

                // تبدیل ساده (این فقط برای نمایش است)
                // در پروژه واقعی از کتابخانه مناسب استفاده کنید
                return `${year}/${month}/${day}`;
            }
        } catch (e) {
            console.error("Error in date conversion:", e);
        }
        return dateStr;
    }

    /**
     * فرمت تاریخ به YYYY-MM-DD
     */
    function formatDate(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    /**
     * تبدیل تاریخ به ساعت و دقیقه
     */
    function convertDateTohhmm(dateTime) {
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
     * ساخت آبجکت JSON برای جزئیات پیام
     */
    function makeJsonObjectForMessateDetails(message) {
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
     * اضافه کردن شناسه فایل به input مخفی
     */
    function addFileIdToHiddenInput(fileId, selector) {
        const $input = $(selector);
        if (!$input.length) {
            console.error("Hidden input not found:", selector);
            return;
        }

        let currentValue = $input.val();
        let fileIds = currentValue ? currentValue.split(',').filter(id => id.trim() !== '') : [];

        if (!fileIds.includes(fileId.toString())) {
            fileIds.push(fileId.toString());
            $input.val(fileIds.join(','));
            console.log(`File ID ${fileId} added to ${selector}`);
        }
    }

    /**
     * حذف شناسه فایل از input مخفی
     */
    function removeFileIdFromHiddenInput(fileId, selector) {
        const $input = $(selector);
        if (!$input.length) {
            console.error("Hidden input not found:", selector);
            return;
        }

        let currentValue = $input.val();
        let fileIds = currentValue ? currentValue.split(',').filter(id => id.trim() !== '') : [];

        const index = fileIds.indexOf(fileId.toString());
        if (index > -1) {
            fileIds.splice(index, 1);
            $input.val(fileIds.join(','));
            console.log(`File ID ${fileId} removed from ${selector}`);
        }
    }

    /**
     * بررسی وجود فایل در input مخفی
     */
    function hasFileIdInHiddenInput(fileId, selector) {
        const $input = $(selector);
        if (!$input.length) {
            return false;
        }

        let currentValue = $input.val();
        let fileIds = currentValue ? currentValue.split(',').filter(id => id.trim() !== '') : [];

        return fileIds.includes(fileId.toString());
    }

    /**
     * دریافت تمام شناسه‌های فایل از input مخفی
     */
    function getFileIdsFromHiddenInput(selector) {
        const $input = $(selector);
        if (!$input.length) {
            return [];
        }

        let currentValue = $input.val();
        return currentValue ? currentValue.split(',').filter(id => id.trim() !== '').map(id => parseInt(id)) : [];
    }

    /**
     * پاک کردن تمام شناسه‌های فایل از input مخفی
     */
    function clearFileIdsFromHiddenInput(selector) {
        const $input = $(selector);
        if ($input.length) {
            $input.val('');
            console.log(`Cleared all file IDs from ${selector}`);
        }
    }

    /**
     * ایجاد شناسه یکتا برای پیام موقت
     */
    function generateClientMessageId() {
        return 'temp_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    }

    /**
     * بررسی اعتبار فرمت فایل
     */
    function isValidFileType(fileName, allowedExtensions) {
        if (!fileName || !allowedExtensions || !Array.isArray(allowedExtensions)) {
            return false;
        }

        const extension = fileName.split('.').pop().toLowerCase();
        return allowedExtensions.includes(extension);
    }

    /**
     * نمایش loader
     */
    function showLoader(selector, message = 'در حال بارگذاری...') {
        const $element = $(selector);
        if ($element.length) {
            $element.html(`<div class="loader text-center my-5">${message}</div>`);
        }
    }

    /**
     * مخفی کردن loader
     */
    function hideLoader(selector, defaultContent = '') {
        const $element = $(selector);
        if ($element.length && $element.find('.loader').length) {
            $element.html(defaultContent);
        }
    }

    /**
     * ایجاد debounce برای جلوگیری از فراخوانی مکرر
     */
    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    /**
     * ایجاد throttle برای محدود کردن فراخوانی
     */
    function throttle(func, limit) {
        let inThrottle;
        return function () {
            const args = arguments;
            const context = this;
            if (!inThrottle) {
                func.apply(context, args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        };
    }


    // =================================================
    //                 PUBLIC API
    // =================================================

    return {
        // تبدیل تاریخ
        convertGregorianToJalaaliSimple: convertGregorianToJalaaliSimple,
        formatDate: formatDate,
        convertDateTohhmm: convertDateTohhmm,
        extractTime: extractTime,

        // مدیریت فایل‌ها
        formatFileSize: formatFileSize,
        formatAudioTime: formatAudioTime,
        addFileIdToHiddenInput: addFileIdToHiddenInput,
        removeFileIdFromHiddenInput: removeFileIdFromHiddenInput,
        hasFileIdInHiddenInput: hasFileIdInHiddenInput,
        getFileIdsFromHiddenInput: getFileIdsFromHiddenInput,
        clearFileIdsFromHiddenInput: clearFileIdsFromHiddenInput,
        isValidFileType: isValidFileType,

        // JSON و داده‌ها
        makeJsonObjectForMessateDetails: makeJsonObjectForMessateDetails,
        generateClientMessageId: generateClientMessageId,

        // UI helpers
        showLoader: showLoader,
        hideLoader: hideLoader,

        // توابع عملکردی
        debounce: debounce,
        throttle: throttle,

        // توابع کمکی عمومی
        isEmpty: function (obj) {
            if (obj === null || obj === undefined) return true;
            if (Array.isArray(obj)) return obj.length === 0;
            if (typeof obj === 'string') return obj.trim().length === 0;
            if (typeof obj === 'object') return Object.keys(obj).length === 0;
            return false;
        },

        isNumber: function (value) {
            return typeof value === 'number' && !isNaN(value) && isFinite(value);
        },

        deepClone: function (obj) {
            return JSON.parse(JSON.stringify(obj));
        },

        // تابع برای لاگ کردن با timestamp
        logWithTime: function (...args) {
            const timestamp = new Date().toISOString();
            console.log(`[${timestamp}]`, ...args);
        },

        // تابع برای خطا با timestamp
        errorWithTime: function (...args) {
            const timestamp = new Date().toISOString();
            console.error(`[${timestamp}]`, ...args);
        },

        
        getScrollTop: function (element) {
            if (!element) return 0;

            // اگر jQuery object است
            if (typeof element.scrollTop === 'function') {
                return element.scrollTop();
            }
            // اگر DOM element است
            else if (typeof element.scrollTop === 'number') {
                return element.scrollTop;
            }
            // اگر selector string است
            else if (typeof element === 'string') {
                const el = document.querySelector(element);
                return el ? el.scrollTop : 0;
            }

            return 0;
        },

        setScrollTop: function (element, value) {
            if (!element) return;

            // اگر jQuery object است
            if (typeof element.scrollTop === 'function') {
                element.scrollTop(value);
            }
            // اگر DOM element است
            else if (typeof element.scrollTop === 'number') {
                element.scrollTop = value;
            }
            // اگر selector string است
            else if (typeof element === 'string') {
                const el = document.querySelector(element);
                if (el) el.scrollTop = value;
            }
        },


    };

})();