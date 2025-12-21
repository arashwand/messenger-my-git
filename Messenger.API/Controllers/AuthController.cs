using Messenger.Services.Configuration;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Messenger.API.Controllers
{
    /// <summary>
    /// این کنترلر نباید در مستندات Swagger نمایش داده شود
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    public class AuthController : Controller
    {
        private readonly JwtSettings _jwtSettings;
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserService userService, IOptions<JwtSettings> jwtSettings, ILogger<AuthController> logger)
        {
            _jwtSettings = jwtSettings.Value;
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// login
        /// </summary>
        /// <param name="returnUrl"></param>
        /// <returns></returns>
        [HttpGet]
        [AllowAnonymous] 
        public IActionResult Login_ma(string returnUrl = "/")
        {
            // یک فرم لاگین ساده با HTML برمی‌گردانیم
            var html = @"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Login</title>
                    <style>
                        body { font-family: sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; background-color: #f0f2f5; }
                        form { background: white; padding: 2rem; border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1); }
                        input { display: block; width: 100%; padding: 0.5rem; margin-bottom: 1rem; border: 1px solid #ccc; border-radius: 4px; }
                        button { width: 100%; padding: 0.7rem; background-color: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; }
                        h2 { text-align: center; }
                    </style>
                </head>
                <body>
                    <form method='post' action='/Auth/Login_Ma'>
                        <h2>Login...</h2>
                        <input type='hidden' name='ReturnUrl' value='" + returnUrl + @"' />
                        <label>Username</label>
                        <input type='text' name='username' required />
                        <label>Password</label>
                        <input type='password' name='password' required />
                        <button type='submit'>Log In</button>
                    </form>
                </body>
                </html>";
            return Content(html, "text/html");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="returnUrl"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login_Ma(string username, string password, string returnUrl = "/swagger")
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Login attempt with empty username or password.");
                return Content("Username and password are required.");
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

                var credentials = new
                {
                    LoginCode = username,
                    Password = password,
                    Audience = "webApp"
                };

                var content = new StringContent(JsonConvert.SerializeObject(credentials), Encoding.UTF8, "application/json");
                var requestUri = ($"{_jwtSettings.Issuer}Api/Auth/login");

                _logger.LogDebug("Sending auth request for user {Username} to {RequestUri}", username, requestUri);

                var response = await client.PostAsync(requestUri, content);

                if (!response.IsSuccessStatusCode)
                {
                    var respText = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Authentication service returned {StatusCode} for user {Username}. Response: {ResponseText}", response.StatusCode, username, respText);
                    return Content("Invalid credentials or authentication service returned an error.");
                }

                var tokenResponseText = await response.Content.ReadAsStringAsync();

                ResponseModel? responseModel;
                try
                {
                    responseModel = JsonConvert.DeserializeObject<ResponseModel>(tokenResponseText);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Failed to deserialize authentication response for user {Username}. Response: {ResponseText}", username, tokenResponseText);
                    return Content("Unexpected response from authentication service.");
                }

                if (responseModel == null || string.IsNullOrWhiteSpace(responseModel.AccessToken))
                {
                    _logger.LogWarning("Authentication service returned empty token for user {Username}. RawResponse: {ResponseText}", username, tokenResponseText);
                    return Content("Authentication failed.");
                }

                var userModel = GetUserInfoFromToken(responseModel.AccessToken);
                if (userModel == null)
                {
                    _logger.LogWarning("Failed to extract user info from token for user {Username}", username);
                    return Content("Failed to read user information from token.");
                }

                var isValidManager = string.Equals(userModel.RoleName, ConstRoles.Manager, StringComparison.OrdinalIgnoreCase);

                if (isValidManager)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, username),
                        new Claim(ClaimTypes.Role, ConstRoles.Manager) // نقش مدیر را به او می‌دهیم
                    };

                    var claimsIdentity = new ClaimsIdentity(
                        claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    var authProperties = new AuthenticationProperties
                    {
                        // اگر می‌خواهید لاگین با بستن مرورگر از بین نرود
                        // IsPersistent = true, 
                        RedirectUri = returnUrl
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    _logger.LogInformation("Manager {Username} signed in successfully.", username);
                    return LocalRedirect(returnUrl);
                }
                else
                {
                    _logger.LogInformation("User {Username} attempted to access manager route but has role {Role}.", username, userModel.RoleName);
                    return Content("Access denied to this route!");
                }
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                // Timeout
                _logger.LogError(ex, "Timeout calling authentication service for user {Username}", username);
                return Content("Authentication service timed out. Please try again later.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error calling authentication service for user {Username}", username);
                return Content("Error connecting to authentication service. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for user {Username}", username);
                return Content("An unexpected error occurred. Please try again later.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }


        /// <summary>
        /// کافیه اکسس توکن دریافتی رو بهش بدیم
        /// </summary>
        /// <returns></returns>
        private UserInfoVM? GetUserInfoFromToken(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                string? name = jwtToken.Claims.FirstOrDefault(c => c.Type == "NameFamily")?.Value;
                string? roleName = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(roleName) || string.IsNullOrWhiteSpace(userIdClaim))
                {
                    _logger.LogWarning("Token is missing required claims (role or name identifier).");
                    return null;
                }

                if (!int.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogWarning("UserId claim is not a valid int: {UserIdClaim}", userIdClaim);
                    return null;
                }

                int portalRoleId = TryGetIntClaim(jwtToken, "UserId", -1);
                int branchId = TryGetIntClaim(jwtToken, "BranchId", -1);
                int personelId = TryGetIntClaim(jwtToken, "PersonelId", -1);
                int teacherId = TryGetIntClaim(jwtToken, "TeacherId", -1);
                int studentId = TryGetIntClaim(jwtToken, "StudentId", -1);
                int mentorId = TryGetIntClaim(jwtToken, "MentorId", -1);

                var userModel = new UserInfoVM
                {
                    accessToken = token,
                    FullName = name,
                    RoleName = roleName,
                    RoleTitle = name,
                    UserId = userId,
                    PortalRoleId = portalRoleId,
                    BranchId = branchId,
                    MentorId = mentorId,
                    PersonelId = personelId,
                    StudentId = studentId,
                    TeacherId = teacherId
                };

                return userModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse JWT token.");
                return null;
            }
        }

        private static int TryGetIntClaim(JwtSecurityToken jwtToken, string claimType, int defaultValue)
        {
            var claimValue = jwtToken.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
            if (int.TryParse(claimValue, out var val))
                return val;
            return defaultValue;
        }

        private class UserInfoVM
        {
            public string? FullName { get; set; }
            public string? RoleName { get; set; }
            public string? RoleTitle { get; set; }
            public int UserId { get; set; }
            public int PortalRoleId { get; set; }
            public string accessToken { get; set; }
            public int BranchId { get; set; }
            public int PersonelId { get; set; } = -1;
            public int TeacherId { get; set; } = -1;
            public int StudentId { get; set; } = -1;
            public int MentorId { get; set; } = -1;
        }

        private class ResponseModel
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public DateTime Expires { get; set; }
        }
    }
}
