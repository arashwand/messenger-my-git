using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Messenger.WebApp.Models;
using Messenger.WebApp.Helpers;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace Messenger.WebApp.Middlewares
{
    public class TokenRefreshMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenRefreshMiddleware> _logger;

        public TokenRefreshMiddleware(RequestDelegate next, ILogger<TokenRefreshMiddleware> logger)
        {
            _next = next;
            _logger = logger;
            _logger.LogDebug("TokenRefreshMiddleware constructed.");
        }

        /// <summary>  
        /// در هر درخواست کاربر بررسی میکند ایا اعتبار دارد یا خیر و اقدام به ریفرش توکن در صورت لزوم میکند  
        /// </summary>  
        /// <param name="context"></param>  
        /// <returns></returns>  
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                _logger.LogDebug("InvokeAsync started for path: {Path}", context.Request.Path);

                // لیست آدرس‌هایی که نیاز به بررسی ندارند  
                var excludedPaths = new[] { "/Account/Login", "/Account/Register", "/api" };

                // لیست فایل‌های ثابت که نیاز به بررسی ندارند  
                var staticFileExtensions = new[] { ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2", ".ttf", ".eot" };

                // اگر آدرس درخواست در لیست است یا فایل ثابت است، متد بعدی را صدا بزن و بررسی نکن  
                if (excludedPaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase)) ||
                    staticFileExtensions.Any(ext => context.Request.Path.Value.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("Request path excluded from token check: {Path}", context.Request.Path);
                    await _next(context);
                    return;
                }

                var token = context.Request.Cookies["AuthToken"];
                var refreshTokenInLoginCookie = context.Request.Cookies["RefreshToken"];

                // اگر هر دو توکن وجود ندارند، به صفحه لاگین هدایت شود
                if (string.IsNullOrEmpty(token) && string.IsNullOrEmpty(refreshTokenInLoginCookie))
                {
                    _logger.LogInformation("AuthToken and RefreshToken cookies are missing for request {Path}.", context.Request.Path);
                    await RedirectToLogin(context);
                    return;
                }

                // اگر AccessToken وجود ندارد ولی RefreshToken موجود است، اقدام به دریافت توکن جدید کند
                if (string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(refreshTokenInLoginCookie))
                {
                    _logger.LogInformation("AuthToken is missing but RefreshToken exists. Attempting to refresh for request {Path}.", context.Request.Path);
                    var tokenHandler = new JwtSecurityTokenHandler();
                    await TryToNewTokenByReftreshToken(context, tokenHandler, refreshTokenInLoginCookie, null);

                    // بررسی اینکه آیا ریفرش موفق بود (کوکی جدید ست شده یا نه)
                    if (context.Response.HasStarted)
                    {
                        return; // اگر ریدایرکت شده، ادامه نده
                    }

                    await _next(context);
                    return;
                }

                if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(refreshTokenInLoginCookie))
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    JwtSecurityToken jwtToken = null;
                    try
                    {
                        jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
                        _logger.LogDebug("Access token parsed successfully. ValidTo: {ValidTo}", jwtToken?.ValidTo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse access token for request {Path}. Will continue without parsing.", context.Request.Path);
                    }

                    var expiration = jwtToken?.ValidTo ?? DateTime.MinValue;

                    // Log the comparison values to help debugging why refresh may not trigger
                    var threshold = DateTime.UtcNow.AddHours(3.5).AddMinutes(5);
                    _logger.LogDebug("Access token expiration: {ExpirationUtc}; comparison threshold: {ThresholdUtc}", expiration, threshold);

                    if (expiration <= DateTime.UtcNow.AddHours(3.5).AddMinutes(5)) // اگر کمتر از ۵ دقیقه به انقضا مانده باشد  
                    {
                        _logger.LogInformation("Access token is near expiry (or expired). Attempting refresh for request {Path}.", context.Request.Path);
                        // درخواست ریفرش توکن  
                        await TryToNewTokenByReftreshToken(context, tokenHandler, refreshTokenInLoginCookie, token);
                    }

                }

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in TokenRefreshMiddleware.InvokeAsync for path {Path}", context.Request.Path);
                throw;
            }
        }

        private async Task TryToNewTokenByReftreshToken(HttpContext context, JwtSecurityTokenHandler tokenHandler,
            string refreshTokenInLoginCookie, string token)
        {
            try
            {
                _logger.LogDebug("TryToNewTokenByReftreshToken called for path {Path}. Refresh cookie present: {HasRefreshCookie}", context.Request.Path, !string.IsNullOrEmpty(refreshTokenInLoginCookie));

                // درخواست ریفرش توکن  
                var refreshToken = PasswordHelper.Decrypt(refreshTokenInLoginCookie);
                if (string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogWarning("Refresh token cookie decrypted to empty for request {Path}. Redirecting to login.", context.Request.Path);
                    await RedirectToLogin(context);
                    return;
                }

                _logger.LogDebug("Refresh token decrypted. (value omitted for security)");

                // بررسی زمان انقضای RefreshToken از خود توکن
                JwtSecurityToken jwtRefreshToken = null;
                DateTime expirationRefreshToken = DateTime.MinValue;

                try
                {
                    jwtRefreshToken = tokenHandler.ReadToken(refreshToken) as JwtSecurityToken;
                    expirationRefreshToken = jwtRefreshToken?.ValidTo ?? DateTime.MinValue;
                    _logger.LogDebug("Refresh token parsed as JWT. ValidTo: {ValidTo}", expirationRefreshToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse refresh token as JWT for request {Path}.", context.Request.Path);
                    // اگر پارس نشد، فرض کنیم منقضی شده
                    await RedirectToLogin(context);
                    return;
                }

                // بررسی اینکه آیا RefreshToken منقضی شده یا نه
                if (expirationRefreshToken <= DateTime.UtcNow)
                {
                    _logger.LogInformation("Refresh token is expired (ValidTo: {ValidTo} <= UTC now). Redirecting to login.", expirationRefreshToken);
                    await RedirectToLogin(context);
                    return;
                }

                _logger.LogInformation("Refresh token is valid (ValidTo: {ValidTo}). Calling RefreshTokenAsync.", expirationRefreshToken);
                var newToken = await RefreshTokenAsync(refreshToken);
                if (!string.IsNullOrEmpty(newToken))
                {
                    _logger.LogDebug("Refresh endpoint returned content length {Length}", newToken.Length);
                    //--ابتدا پاسخ دریافتی را تبدیل به مدل پاسخ میکنیم  
                    ResponseModel? responseModel = null;
                    try
                    {
                        responseModel = JsonConvert.DeserializeObject<ResponseModel>(newToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize refresh response for request {Path}.", context.Request.Path);
                    }

                    if (responseModel != null && responseModel.AccessToken != null && responseModel.RefreshToken != null)
                    {
                        _logger.LogInformation("Received new access and refresh tokens from refresh endpoint. Setting cookies.");

                        //  ابتدا کوکی های قبلی حذف میشوند  
                        context.Response.Cookies.Delete("AuthToken");
                        context.Response.Cookies.Delete("RefreshToken");

                        // ذخیره اکسس توکن در کوکی  
                        context.Response.Cookies.Append("AuthToken", responseModel.AccessToken, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            Expires = responseModel.Expires
                        });

                        // ذخیره ریفرش توکن در کوکی بصورت انکریپت شده  
                        // انکریپت کردن ریفرش توکن  
                        var refreshTokenEncrypted = PasswordHelper.Encrypt(responseModel.RefreshToken);
                        context.Response.Cookies.Append("RefreshToken", refreshTokenEncrypted, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            Expires = DateTimeOffset.UtcNow.AddDays(7) //--ریفرش توکن ما 7 روز اعتبار دارد  
                        });

                        _logger.LogDebug("Cookies updated successfully for request {Path}.", context.Request.Path);
                    }
                    else
                    {
                        _logger.LogWarning("Refresh response did not contain expected tokens. Redirecting to login. ResponseModel null or missing tokens.");
                        //--مشکل دریافت ریفرش توکن. باید دوباره لاگین کند  
                        await RedirectToLogin(context);
                        return;
                    }
                }
                else
                {
                    _logger.LogWarning("RefreshTokenAsync returned null (refresh failed) for request {Path}. Redirecting to login.", context.Request.Path);
                    // مدیریت عدم موفقیت در ریفرش توکن، مثلاً هدایت به صفحه لاگین  
                    await RedirectToLogin(context);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in TryToNewTokenByReftreshToken for path {Path}. Redirecting to login.", context.Request.Path);
                await RedirectToLogin(context);
            }
        }


        private async Task<string> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                _logger.LogDebug("RefreshTokenAsync: sending refresh request to SSO endpoint. (refresh token omitted)");
                // منطق ریفرش توکن
                using (var client = new HttpClient())
                {
                    // ارسال مدل RefreshRequest با RefreshToken و Audience
                    var requestBody = new
                    {
                        RefreshToken = refreshToken,
                        Audience = "webApp"
                    };

                    var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                    string uri = "https://sso.iran-europe.net/api/auth/refresh";

//#if DEBUG
//                    string uri = "https://localhost:7100/api/auth/refresh"; // آدرس لوکال در حالت دیباگ
//#else
//                                    string uri = "https://sso.iran-europe.net/api/auth/refresh"; // آدرس اصلی در حالت غیر دیباگ
//#endif

                    var response = await client.PostAsync(uri, content);

                    _logger.LogDebug("Refresh endpoint returned status code {StatusCode} for URI {Uri}.", response.StatusCode, uri);

                    if (response.IsSuccessStatusCode)
                    {
                        var newToken = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("RefreshTokenAsync succeeded. Response length: {Length}", newToken.Length);
                        return newToken;
                    }
                    else
                    {
                        _logger.LogWarning("RefreshTokenAsync failed. StatusCode: {StatusCode}", response.StatusCode);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while calling refresh endpoint");
                return null;
            }
        }

        private async Task RedirectToLogin(HttpContext context)
        {
            // build returnUrl from current request path + query
            var pathAndQuery = context.Request.Path + context.Request.QueryString;
            var returnUrl = Uri.EscapeDataString(pathAndQuery);

            _logger.LogDebug("Redirecting to login for path {Path} (returnUrl: {ReturnUrl})", context.Request.Path, returnUrl);

            // For AJAX/JSON requests, return 401 so client-side can handle redirect
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                context.Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                _logger.LogInformation("Returning 401 JSON response for AJAX/JSON request at {Path}", context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                var payload = JsonConvert.SerializeObject(new { success = false, message = "Unauthorized" });
                await context.Response.WriteAsync(payload);
                return;
            }

            context.Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
        }
    }
}