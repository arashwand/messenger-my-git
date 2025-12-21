using Messenger.Moderation;
using Messenger.WebApp.ServiceHelper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Messenger.WebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "AdminApiPolicy")]
    
    public class BadWordsController : ControllerBase
    {
        private readonly RedisWordManager _redisManager;
        private readonly IWebHostEnvironment _env;

        public BadWordsController(RedisWordManager redisManager, IWebHostEnvironment env)
        {
            _redisManager = redisManager;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var words = await _redisManager.GetWordsFromRedisAsync();
            return Ok(words);
        }

        [HttpPost("add")]
        public async Task<IActionResult> Add([FromBody] string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return BadRequest();

            await _redisManager.AddWordAsync(word);
            return Ok(new { Message = $"Word '{word}' added via Redis." });
        }

        [HttpDelete("delete/{word}")]
        public async Task<IActionResult> Delete(string word)
        {
            await _redisManager.RemoveWordAsync(word);
            return Ok(new { Message = $"Word '{word}' removed via Redis." });
        }

        [HttpPost("import-json")]
        public async Task<IActionResult> ImportFromJson()
        {
            try
            {
                // فراخوانی متد جدید که همه کارها را انجام می‌دهد
                await _redisManager.ForceImportJsonAsync();

                return Ok(new { Message = "JSON file imported to Redis and all servers notified." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }


        // ۱. دانلود فایل همیشه به‌روز (On-Demand Generation)
        // این متد فایل فیزیکی را نمی‌خواند، بلکه دیتا را از ردیس می‌گیرد و فایل تولید می‌کند
        [HttpGet("download")]
        public async Task<IActionResult> DownloadJson()
        {
            // دریافت آخرین دیتا از Redis
            var words = await _redisManager.GetWordsFromRedisAsync();

            // مرتب‌سازی الفبایی برای زیبایی فایل
            words.Sort();

            // تبدیل به JSON
            var jsonBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                words,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
            );

            // ساخت نام فایل با تاریخ و زمان
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var fileName = $"badwords_{timestamp}.json";

            // ارسال به عنوان فایل قابل دانلود
            return File(jsonBytes, "application/json", fileName);
        }

        // ۲. سینک کردن فایل فیزیکی روی سرور (Optional / Manual Sync)
        // هر وقت خواستید فایل فیزیکی روی سرور هم آپدیت شود، این را صدا بزنید
        //معمولا لازم نمیشه اما اگه مسئول سرور خواست بدون دانلود این رو برداره، بهتره اول این متد صدا زده بشه
        [HttpPost("sync-to-disk")]
        public async Task<IActionResult> SyncToDisk()
        {
            try
            {
                var words = await _redisManager.GetWordsFromRedisAsync();
                words.Sort();

                var filePath = Path.Combine(_env.ContentRootPath, "Data", "badwords.json");

                // نوشتن روی دیسک (Async)
                await System.IO.File.WriteAllTextAsync(
                    filePath,
                    System.Text.Json.JsonSerializer.Serialize(words, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
                );

                return Ok(new { Message = "Local JSON file updated from Redis successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Could not write to disk.", Detail = ex.Message });
            }
        }
    }
}
