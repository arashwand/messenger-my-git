using Messenger.DTOs;
using Messenger.Services.Interfaces.External;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Services.External
{
    public class ExternalTokenProvider : IExternalTokenProvider
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ExternalTokenProvider> _logger;
        private readonly IConfiguration _configuration;


        public ExternalTokenProvider(HttpClient httpClient, IMemoryCache cache,
            ILogger<ExternalTokenProvider> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<string> GetTokenAsync()
        {
            if (_cache.TryGetValue("external_jwt", out string token))
                return token;

            token = await RequestSsoTokenAsync();

            if (string.IsNullOrEmpty(token))
                throw new Exception("Failed to retrieve token from SSO service.");

            _cache.Set("external_jwt", token,
                absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(50));

            return token;
        }

        private async Task<string> RequestSsoTokenAsync()
        {
            try
            {
                var ssoSettings = _configuration.GetSection("SsoSettings");
                var clientId = ssoSettings["ClientId"];
                var clientSecret = ssoSettings["ClientSecret"];
                var audience = ssoSettings["Audience"];
                var tokenEndpoint = ssoSettings["TokenEndpoint"];

                // ۱. ساخت توکن Basic Authentication
                var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                
                // استفاده از HttpRequestMessage برای جلوگیری از تغییر DefaultRequestHeaders
                using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);//Bearer or Basic

                // ۲. بدنه درخواست فقط شامل grant_type و scope خواهد بود
                var requestData = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["Scope"] = ssoSettings["Scope"],
                    ["Audience"] = audience
                };
                request.Content = new FormUrlEncodedContent(requestData);
                //var response = await ssoClient.PostAsync(ssoSettings["TokenEndpoint"], new FormUrlEncodedContent(requestData));
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("SSO token request failed with status: {StatusCode}", response.StatusCode);
                    return null;
                }
                
                var responseContent = await response.Content.ReadFromJsonAsync<SsoTokenResponse>();
                var token = responseContent?.access_token;

                _logger.LogInformation("Successfully obtained access token from SSO.");
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while requesting SSO token.");
                return null;
            }
        }
    }
}
