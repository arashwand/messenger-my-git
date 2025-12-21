using Messenger.DTOs;
using Messenger.Services.Interfaces.External;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Services.External
{
    public class UserExternalApi : IUserExternalApi
    {
        private readonly HttpClient _httpClient;
        private readonly IExternalTokenProvider _externalTokenProvider;

        public UserExternalApi(HttpClient httpClient, IExternalTokenProvider externalTokenProvider)
        {
            _httpClient = httpClient;
            _externalTokenProvider = externalTokenProvider;
        }

        /// <summary>
        /// توسط ایدی کاربر ، مشخصات او را از سرویس خارجی دریافت می‌کند
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<ResponseUserinfoDto?> GetUserByIdAsync(long userId)
        {
            try
            {
                var token = await _externalTokenProvider.GetTokenAsync();

                // Set the Authorization header with Bearer scheme for JWT token
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync($"api/users/{userId}");

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<ResponseUserinfoDto>();
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
