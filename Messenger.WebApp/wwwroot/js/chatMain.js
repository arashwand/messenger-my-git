// =========================================================================
// MAIN ENTRY POINT
// =========================================================================
// نقطه ورود اصلی برنامه - بارگذاری و تنظیم ماژول‌ها
// =========================================================================

(function ($) {
    'use strict';

    // تابع برای بررسی بارگذاری ماژول‌ها
    function checkModulesLoaded() {
        const requiredModules = [
            'chatApp',
            'chatMessageManager',
            'chatUIRenderer',
            'chatSignalRHandlers',
            'chatUtils'
        ];

        const missingModules = [];

        requiredModules.forEach(moduleName => {
            if (!window[moduleName]) {
                missingModules.push(moduleName);
            }
        });

        if (missingModules.length > 0) {
            console.error(`Missing required modules: ${missingModules.join(', ')}`);
            return false;
        }

        console.log("All required modules loaded successfully.");
        return true;
    }

    // تابع اصلی راه‌اندازی
    function initializeChatApp() {
        // بررسی بارگذاری ماژول‌ها
        if (!checkModulesLoaded()) {
            console.error("Cannot initialize chat app - required modules are missing.");
            return;
        }

        // راه‌اندازی ماژول‌های کمکی
        if (window.chatMessageManager && window.chatMessageManager.init) {
            window.chatMessageManager.init();
        }

        // راه‌اندازی ماژول اصلی
        if (window.chatApp && window.chatApp.init) {
            window.chatApp.init();
        } else {
            console.error("chatApp.init not found!");
        }

        console.log("Chat application initialized successfully.");
    }

    // صبر تا بارگذاری کامل DOM
    $(document).ready(function () {
        console.log("DOM ready, initializing chat application...");

        // تأخیر کوچک برای اطمینان از بارگذاری کامل اسکریپت‌ها
        setTimeout(initializeChatApp, 100);
    });

    // اضافه کردن event listener برای beforeunload (ذخیره وضعیت)
    window.addEventListener('beforeunload', function (e) {
        if (window.chatApp && window.chatApp.disconnect) {
            // می‌توانید وضعیت را ذخیره کنید یا cleanup انجام دهید
            console.log("Page unloading, performing cleanup...");

            // قطع اتصال SignalR
            window.chatApp.disconnect().then(() => {
                console.log("SignalR connection disconnected successfully.");
            }).catch(err => {
                console.error("Error disconnecting SignalR:", err);
            });
        }
    });

    // افزودن تابع initialize به window برای دسترسی از خارج
    window.initializeChat = initializeChatApp;

})(jQuery);