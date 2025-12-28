# Load Balancing Dashboard

## دسترسی به داشبورد

داشبورد Load Balancing در مسیر زیر قابل دسترسی است:

```
/LoadBalancingDashboard
```

**مثال:**
```
https://your-domain.com/LoadBalancingDashboard
```

## محدودیت دسترسی

- فقط کاربران با نقش **Manager** به این داشبورد دسترسی دارند
- احراز هویت از طریق سیستم JWT/Cookie authentication انجام میشود

## ویژگیها

### 1. **نمایش متریکهای سیستم (Real-time)**
- استفاده از CPU (با نمایش درصد و progress bar)
- استفاده از حافظه (Memory Usage)
- تعداد کاربران آنلاین (Active Connections)
- امتیاز بار سیستم (System Load Score با وزندهی)

### 2. **آمار صف Hangfire**
- تعداد پیامهای در صف (Enqueued)
- تعداد پیامهای در حال پردازش (Processing)
- تعداد پیامهای موفق (Succeeded)
- تعداد پیامهای ناموفق (Failed)
- تعداد پیامهای زمانبندی شده (Scheduled)
- تعداد پیامهای حذف شده (Deleted)

### 3. **تفکیک صفها بر اساس اولویت**
جدول نمایش تعداد پیامها در هر صف:
- Critical (بحرانی)
- High (بالا)
- Default (عادی)
- Low (پایین)

### 4. **به‌روزرسانی خودکار**
- متریکهای سیستم هر 5 ثانیه به صورت خودکار بروزرسانی میشوند (AJAX)
- بدون نیاز به Refresh صفحه

### 5. **هشدارهای هوشمند**
- در صورت فشار بر سیستم (CPU > 70%, Memory > 75%, یا Connections > 500)
- نمایش Alert در بالای صفحه

### 6. **طراحی Responsive**
- سازگار با موبایل، تبلت و دسکتاپ
- استفاده از Bootstrap 5
- طراحی مدرن با Gradient و Hover Effects

## دکمههای عملیاتی

1. **داشبورد Hangfire**: لینک مستقیم به `/hangfire` برای مشاهده جزئیات بیشتر
2. **بروزرسانی دستی**: امکان Refresh دستی صفحه

## تنظیمات Threshold

مقادیر پیشفرض برای تشخیص فشار سیستم:
- CPU: 70%
- Memory: 75%
- Active Connections: 500

این مقادیر در `SystemMonitorService.cs` قابل تغییر هستند.

## مثال استفاده

1. وارد سیستم با یک کاربر Manager شوید
2. به آدرس `/LoadBalancingDashboard` بروید
3. داشبورد به صورت خودکار متریکها را نمایش میدهد
4. برای مشاهده جزئیات بیشتر روی "داشبورد Hangfire" کلیک کنید

## نکات فنی

- **Backend**: Razor Pages
- **Frontend**: Bootstrap 5 + Font Awesome 6
- **Real-time Updates**: JavaScript Fetch API
- **Authorization**: ASP.NET Core Authorization با Role-based access
- **Caching**: متریکها برای 5 ثانیه Cache میشوند
