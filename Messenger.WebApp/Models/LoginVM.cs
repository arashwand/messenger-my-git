using System.ComponentModel.DataAnnotations;

namespace Messenger.WebApp.Models
{
    public class LoginVM
    {
        [Required(ErrorMessage = "کد ملی یا کد دانشجویی")]
        public string LoginCode { get; set; } = null!;

        [Required(ErrorMessage = "لطفا رمز عبور را وارد نمایید")]
        public string Password { get; set; } = null!;
    }
}
