using Hangfire;
using Hangfire.SqlServer;
using Messenger.API.Helper;
using Messenger.API.Hubs;
using Messenger.API.ServiceHelper;
using Messenger.API.ServiceHelper.Interfaces; // Added for Redis service interfaces
using Messenger.API.Services;
using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services; // Added for ChatHub
using Messenger.Services.Classes;
using Messenger.Services.Configuration;
using Messenger.Services.Interfaces;
using Messenger.Services.Interfaces.External;
using Messenger.Services.Services;
using Messenger.Services.Services.External;
using Messenger.Tools;
using Messenger.WebApp.ServiceHelper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Reflection;
using System.Text;


var builder = WebApplication.CreateBuilder(args);
var ConnectionString = builder.Configuration["Settings:ConnectionString"];
var redisConfig = builder.Configuration["Redis:HostConfig"];
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
var ssoSettings = builder.Configuration.GetSection("SsoSettings");
var ssoTokenEndpoint = builder.Configuration["SsoSettings:TokenEndpoint"];

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

builder.Services.Configure<TimeSettingOptions>(
    builder.Configuration.GetSection(TimeSettingOptions.SectionName));

// Create Configuration variable to read from appSettings.json  
var Configuration = builder.Configuration;
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // Add Razor Pages support for Load Balancing Dashboard

builder.Services.AddMemoryCache();

builder.Services.AddDbContext<IEMessengerDbContext>(options =>
    options.UseSqlServer(ConnectionString,
        o => o.UseCompatibilityLevel(160))); //---برای اینکه در کوئریهای حاوی Contains  به خطا نخوریم، این گزینه را از 120 به 160 در sql 2022  تغیر میدهیم


builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisConfig, true);
    return ConnectionMultiplexer.Connect(configuration);
});


builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConfig, options =>
    {
        options.Configuration.AbortOnConnectFail = false;
    });

// خواندن تنظیمات از appsettings.json
var ftpSettings = builder.Configuration.GetSection("FtpSettings");

// Correctly read AllowedExtensions as a string array from configuration
string[] allowedExtentions = builder.Configuration.GetSection("FileStorage:AllowedExtensions").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddSingleton(new FtpUploader(
    ftpSettings["Host"],
    ftpSettings["Username"],
    ftpSettings["Password"],
    ftpSettings["BaseUploadPath"],
    ftpSettings["BaseUploadPathThumbnails"],
    allowedExtentions
));


builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 100; // 100 MB
});

// 1. Configure JWT Authentication

if (string.IsNullOrEmpty(jwtSettings.Key) || jwtSettings.Key.Length < 32) // Basic check for key length/presence
{
    throw new ArgumentNullException("Jwt:Key", "JWT Key must be configured in appsettings.json and be sufficiently long and complex.");
}


// این سرویس برای دسترسی به HttpContext در سرویس‌های دیگر لازم است
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
{
    // به جای تعیین یک پیش‌فرض ثابت، یک "Policy Scheme" را به عنوان پیش‌فرض تعریف می‌کنیم.
    // این Policy Scheme تصمیم می‌گیرد که از JWT استفاده کند یا از کوکی.
    options.DefaultScheme = "JWT_OR_COOKIE";
    options.DefaultChallengeScheme = "JWT_OR_COOKIE";

    //options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    //options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
// 2. اضافه کردن احراز هویت با کوکی
.AddCookie(options =>
{
    // اگر کاربری که لاگین نکرده، بخواهد به یک صفحه محافظت‌شده (مانند Swagger) برود،
    // به این آدرس هدایت می‌شود تا لاگین کند.
    options.LoginPath = "/Auth/Login_Ma";
    // اگر لاگین کرده ولی نقش مورد نیاز را ندارد، به این آدرس می‌رود.
    options.AccessDeniedPath = "/Auth/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(1); // مدت زمان اعتبار کوکی
    options.SlidingExpiration = true; // با هر درخواست، زمان اعتبار تمدید شود
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudiences = jwtSettings.Audiences,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
    };
    //TODO :  این بلوک بعد نهایی شدن حذف شود
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            // Log detailed authentication failure information
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Exception, "JWT Authentication Failed.");
            Console.WriteLine("API AuthN Failed: " + context.Exception.ToString()); // Also to console for immediate visibility
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("JWT Token Validated for: {User}, Claims: {Claims}",
                context.Principal.Identity?.Name ?? "Unknown",
                string.Join(", ", context.Principal.Claims.Select(c => $"{c.Type}={c.Value}")));
            Console.WriteLine("API AuthN Succeeded for: " + context.Principal.Identity?.Name); // Also to console
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("JWT Challenge issued. AuthenticationScheme: {Scheme}, Error: {Error}, Description: {Description}",
                context.AuthenticateFailure?.TargetSite, // This might give some context
                context.Error,
                context.ErrorDescription);
            Console.WriteLine($"API AuthN Challenge: {context.Error} - {context.ErrorDescription}");

            return Task.CompletedTask;
        }
    };
})
// تعریف Policy Scheme هوشمند
.AddPolicyScheme("JWT_OR_COOKIE", "JWT or Cookie Authentication", options =>
{
    // این بخش، مغز متفکر سیستم است.
    // بر اساس هر درخواست، تصمیم می‌گیرد کدام طرح احراز هویت اجرا شود.
    options.ForwardDefaultSelector = context =>
    {
        // هدر Authorization را بخوان
        string authorization = context.Request.Headers["Authorization"];
        // اگر هدر وجود داشت و با "Bearer " شروع می‌شد، از طرح JWT استفاده کن
        if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer "))
        {
            return JwtBearerDefaults.AuthenticationScheme; // "Bearer"
        }

        // در غیر این صورت (مثلاً درخواستی از مرورگر برای صفحه Swagger)، از طرح کوکی استفاده کن
        return CookieAuthenticationDefaults.AuthenticationScheme; // "Cookies"
    };
});


builder.Services.AddAuthorizationBuilder()
    .AddPolicy("IsBridgeService", policy => policy.RequireClaim("scope", "bridge_service"))
    .AddPolicy("IsPortalAcc", policy => policy.RequireClaim("scope", "system_bot"))
    .AddPolicy("AdminPolicy", policy => policy.RequireRole("Manager"));


// Add services to the container.  
builder.Services.AddControllers();

// Swagger با تنظیم احراز هویت
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Messenger Iran Europe - API",
        Version = "v1",
        Description = "مستندات API فقط برای نقش manager در دسترس است"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "توکن JWT خود را وارد کنید"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

    c.SupportNonNullableReferenceTypes();

    // این خط برای مدل‌های استفاده شده میباشد:
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    // برای لیست فایلها:
    c.MapType<List<IFormFile>>(() => new OpenApiSchema
    {
        Type = "array",
        Items = new OpenApiSchema { Type = "string", Format = "binary" }
    });

    // مسیر فایل XML برای اینکه توضیحات روی کنترلر ها در swagger  نمایش داده شود
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    // (اختیاری اما مفید) اضافه کردن یک OperationFilter برای تعیین multipart/form-data روی اکشن‌هایی که IFormFile دارند
    //c.OperationFilter<FileUploadOperationFilter>();

    //c.OperationFilter<MultipartFormDataOperationFilter>();
});

// تنظیم CORS برای دسترسی کلاینت‌ها
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowWebApp", builder =>
//    {
//        builder.WithOrigins("https://localhost:7260") // آدرس WebApp
//               .AllowAnyMethod()
//               .AllowAnyHeader()
//               .AllowCredentials(); // برای SignalR
//    });
//});

builder.Logging.ClearProviders(); // حذف ارائه‌دهندگان پیش‌فرض
builder.Logging.AddConsole(); // اضافه کردن لاگ به کنسول
builder.Logging.AddDebug(); // اضافه کردن لاگ به Debug
builder.Logging.SetMinimumLevel(LogLevel.Information); // تنظیم سطح حداقل لاگ


builder.Services.AddScoped<IUserService, UserService>();

// ثبت سرویس SSOApi با استفاده از HttpClientFactory
builder.Services.AddHttpClient<IUserExternalApi, UserExternalApi>(client =>
{
    client.BaseAddress = new Uri(jwtSettings.Issuer);
    //client.BaseAddress = new Uri(ssoTokenEndpoint);
});

//builder.Services.AddHttpClient<IExternalTokenProvider, ExternalTokenProvider>(client =>
//{
//    client.BaseAddress = new Uri(jwtSettings.Issuer);
//});

builder.Services.AddSingleton<IExternalTokenProvider, ExternalTokenProvider>();

//TODO: بعدا ازش استفاده بشه برای بررسی سطوح دسترسی
builder.Services.AddScoped<IAccessControlService, AccessControlService>();

builder.Services.AddScoped<IManageUserService, ManageUserService>();
builder.Services.AddScoped<IMessageService, MessageService>(); // MessageService depends on IFileService, IUserService
builder.Services.AddScoped<IChannelService, ChannelService>(); // ChannelService might depend on IUserService
builder.Services.AddScoped<IClassGroupService, ClassGroupService>(); // ClassGroupService might depend on IUserService
builder.Services.AddScoped<IFileManagementService, FileManagementService>();
builder.Services.AddScoped<IFileCleanupService, FileCleanupService>();
builder.Services.AddScoped<IRedisCacheService, RedisCacheService>();
builder.Services.AddScoped<IPersonnelChatAccessService, PersonnelChatAccessService>();
builder.Services.AddScoped<IManagePushService, ManagePushService>();

// Register custom Redis services
builder.Services.AddScoped<RedisLastMessageService>();
builder.Services.AddSingleton<IRedisUserStatusService, RedisUserStatusService>(); // Changed to Singleton for SystemMonitorService
builder.Services.AddSingleton<IRedisUnreadManage, RedisUnreadManage>();

// Register BroadcastService
builder.Services.AddScoped<IBroadcastService, BroadcastService>();

// Register Redis Queue Client
builder.Services.AddSingleton<IRedisQueueClient, RedisQueueClient>();


// سرویس Push
builder.Services.AddScoped<PushService>();

// Hosted Service برای صف Push
builder.Services.AddHostedService<BackgroundPushQueueService>();


// Hosted services (RedisToSqlSyncService was already here, ensure it's correct)
builder.Services.AddHostedService<RedisToSqlSyncService>();
builder.Services.AddHostedService<DeleteFileBackgroundService>();

// background service for sync read\unread messages from Redis to SQL and vice bersa
builder.Services.AddHostedService<UnreadMessageSyncService>();

// Hangfire Configuration
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(ConnectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true,
        SchemaName = "HangfireMessenger"
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = builder.Configuration.GetValue<int>("Hangfire:WorkerCount", 5); // پیشفرض: 5
    options.Queues = builder.Configuration.GetSection("Hangfire:Queues").Get<string[]>() 
        ?? new[] { "critical", "high", "default", "low" };
});

// Register Hangfire services
builder.Services.AddScoped<IMessageQueueService, MessageQueueService>();
builder.Services.AddScoped<ProcessMessageJob>();

// Register System Monitor Service for Load Balancing
builder.Services.AddSingleton<ISystemMonitorService, SystemMonitorService>();

builder.Services.Configure<FileConfigSetting>(builder.Configuration.GetSection("FileStorage"));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi  
builder.Services.AddOpenApi();

// Vapid Key Generation
//if (args.contains("--gen-vapid"))
//{
//    VapidKeyGenerator.PrintToConsole();
//    return;
//}

var app = builder.Build();



// 1. Exception Handling: این باید اولین میان‌افزار باشد تا خطاهای بعدی را بگیرد.
if (app.Environment.IsProduction())
{
    // برای Production: کاربر را به یک صفحه خطای مشخص هدایت می‌کند.
    app.UseExceptionHandler("/Error");
    // HSTS به مرورگرها می‌گوید که همیشه از HTTPS استفاده کنند.
    app.UseHsts();
}
else // اگر در حالت Development هستیم
{
    // برای Development: صفحه جزئیات کامل خطا را نمایش می‌دهد.
    app.UseDeveloperExceptionPage();
    app.UseSwagger(); //--در حالت لوکال در دسترس است -
    app.UseSwaggerUI();//اگه بخواهیم لوکال هم برای دسترسی به این سرویس لاگین کنه، باید این دو خط کامنت شوند
    
}

// 2. HTTPS Redirection: تمام درخواست‌های HTTP را به HTTPS منتقل می‌کند.
app.UseHttpsRedirection();

// 3. Static Files: امکان سرویس‌دهی فایل‌های استاتیک مانند CSS, JS و تصاویر را فراهم می‌کند.
app.UseStaticFiles();

// 4. Routing: تصمیم می‌گیرد که کدام Endpoint باید درخواست را پردازش کند.
app.UseRouting();

// 5. CORS Policy: این باید بین UseRouting و UseAuthorization/MapControllers باشد.
// app.UseCors("AllowWebApp");

// 6. Authentication & Authorization: هویت کاربر را تایید و سطح دسترسی او را بررسی می‌کند.
app.UseAuthentication();
app.UseAuthorization();

// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DashboardTitle = "Messenger Queue Dashboard"
});


app.MapOpenApi();

// فایل swagger.json همیشه موجود باشه
app.UseSwagger();

#region  محدود کردن دسترسی به Swagger UI


app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/swagger"), appBuilder =>
{
    appBuilder.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.IsInRole(ConstRoles.Manager))
        {
            await next();
        }
        else
        {
            context.Response.StatusCode = 403; // Forbidden
            await context.Response.WriteAsync("Access denied. Swagger is restricted to managers.");
        }
    });
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1");
    c.RoutePrefix = "swagger"; // مسیر swagger
});

#endregion

// 7. Endpoints: Endpointهای برنامه (Controllers, Hubs, etc.) را تعریف می‌کند.
app.MapControllers();
app.MapRazorPages(); // Map Razor Pages for Load Balancing Dashboard
app.MapHub<ChatHub>("/chathub");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

