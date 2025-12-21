using Messenger.WebApp.Middlewares;
using Messenger.WebApp.Models;
using Messenger.WebApp.ServiceHelper;
using Messenger.WebApp.ServiceHelper.Interfaces;
using Messenger.WebApp.Hubs; // Added for WebAppChatHub
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Messenger.DTOs;
using Messenger.Moderation;
using Messenger.Tools;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var redisConfig = builder.Configuration["Redis:HostConfig"];
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
var secretKey = jwtSettings.Key;

#region Moderation

// 1. اتصال به Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisConfig, true);
    return ConnectionMultiplexer.Connect(configuration);
});


// 2. ثبت سرویس فیلتر (Trie)
builder.Services.AddSingleton<ProfanityFilter>();

// 3. ثبت منیجر ردیس
builder.Services.AddSingleton<RedisWordManager>();

#endregion


// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddSignalR();

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme; //or JwtBearerDefaults.AuthenticationScheme;
}).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ReturnUrlParameter = "returnUrl";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Name = "Identity-Cookies";

    //options.Cookie.SameSite = SameSiteMode.Strict;
})
.AddJwtBearer(options =>
{
    // Use UTF8 bytes of the configured Key to match SSO signing behavior
    var keyBytes = System.Text.Encoding.UTF8.GetBytes(secretKey);

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ClockSkew = jwtSettings.ClockSkew,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
    };
        
});

// معرفی پالیسی جهت نقش ادمین 
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminPolicy", policy =>
    {
        policy.RequireRole(ConstRoles.Manager);
    })
    .AddPolicy("AdminApiPolicy", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireRole(ConstRoles.Manager);
    });


// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});



var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];

builder.Services.AddHttpClient(); // برای درخواست‌های HTTP  
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthTokenHandler>();

builder.Services.AddHttpClient("UserService", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient("MessageService", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient("ChannelService", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient("ClassGroupService", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient("FileManagementService", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient("ManageUserService", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<AuthTokenHandler>();

// HttpClient for RedisClient
//builder.Services.AddHttpClient("RedisService", client =>
//{
//    client.BaseAddress = new Uri(apiBaseUrl);
//}).AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddScoped<IUserServiceClient, UserServiceClient>(provider =>
   new UserServiceClient(provider.GetRequiredService<IHttpClientFactory>(), "UserService", provider.GetRequiredService<ILogger<UserServiceClient>>()));

builder.Services.AddScoped<IMessageServiceClient, MessageServiceClient>(provider =>
   new MessageServiceClient(provider.GetRequiredService<IHttpClientFactory>(), "MessageService", provider.GetRequiredService<ILogger<MessageServiceClient>>()));

builder.Services.AddScoped<IChannelServiceClient, ChannelServiceClient>(provider =>
   new ChannelServiceClient(provider.GetRequiredService<IHttpClientFactory>(), "ChannelService", provider.GetRequiredService<ILogger<ChannelServiceClient>>()));

builder.Services.AddScoped<IClassGroupServiceClient, ClassGroupServiceClient>(provider =>
   new ClassGroupServiceClient(provider.GetRequiredService<IHttpClientFactory>(), "ClassGroupService", provider.GetRequiredService<ILogger<ClassGroupServiceClient>>()));

builder.Services.AddScoped<IFileManagementServiceClient, FileManagementServiceClient>(provider =>
   new FileManagementServiceClient(provider.GetRequiredService<IHttpClientFactory>(), "FileManagementService", provider.GetRequiredService<ILogger<FileManagementServiceClient>>()));

builder.Services.AddScoped<IManageUserServiceClient, ManageUserServiceClient>(provider =>
   new ManageUserServiceClient(provider.GetRequiredService<IHttpClientFactory>(), "ManageUserService", provider.GetRequiredService<ILogger<ManageUserServiceClient>>()));

// Register ApiSettings
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
builder.Services.Configure<FileConfigSetting>(builder.Configuration.GetSection("FileStorage"));


// ۱. کلاس HubConnectionManager را به عنوان پیاده‌سازی IRealtimeHubBridgeService ثبت کنید
// بقیه بخش‌های برنامه شما (کنترلرها، سرویس‌های دیگر) همچنان IRealtimeHubBridgeService را تزریق می‌کنند
builder.Services.AddSingleton<IRealtimeHubBridgeService, HubConnectionManager>();

// ۲. سرویس Monitor را به عنوان Hosted Service ثبت کنید
builder.Services.AddHostedService<HubConnectionMonitor>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHttpsRedirection();
    app.UseHsts();
}

// 4. راه‌اندازی اولیه (Warm-up)
// ما می‌خواهیم قبل از اینکه اولین درخواست بیاید، لیست لود شده باشد
using (var scope = app.Services.CreateScope())
{
    var redisManager = scope.ServiceProvider.GetRequiredService<RedisWordManager>();
    // بهتر است این کار Async انجام شود، اما در Main ساده‌سازی می‌کنیم
    redisManager.InitializeAsync().Wait();
}

app.UseStaticFiles();
app.UseMiddleware<TokenRefreshMiddleware>();
app.UseRouting();

// UseCors باید قبل از UseAuthentication و UseAuthorization قرار گیرد
// Configure CORS
//app.UseCors("AllowWebApp");

app.UseAuthentication();
app.UseAuthorization();


app.MapStaticAssets();

app.MapAreaControllerRoute(
    name: "Manager",
    areaName: "Manager",
    pattern: "Manager/{controller=ManageUser}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// app.MapHub<ChatHub>("/chatHub"); // This is now handled by Messenger.API
app.MapHub<WebAppChatHub>("/webappchathub"); // Added for the WebApp's own chat hub


app.Run();
