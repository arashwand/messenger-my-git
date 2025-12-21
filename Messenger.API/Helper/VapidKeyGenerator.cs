using WebPush;

namespace Messenger.API.Helper
{
    public static class VapidKeyGenerator
    {
        /// <summary>
        /// برای ایجاد کلید عمومی و خصوصی در ترمینال دستور زیر را بزنید
        /// dotnet run --project Messenger.API -- --gen-vapid
        /// همچنین باید شرط قبل از ایجاد app  را از کامنت خارج کنید
        /// </summary>
        public static void PrintToConsole()
        {
            var keys = VapidHelper.GenerateVapidKeys();
            Console.WriteLine("=== VAPID Keys ===");
            Console.WriteLine("PublicKey:  " + keys.PublicKey);
            Console.WriteLine("PrivateKey: " + keys.PrivateKey);
            Console.WriteLine("=> Store PrivateKey securely (user-secrets or environment).");
        }
    }
}
