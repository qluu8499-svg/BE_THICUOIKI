using System.ComponentModel.DataAnnotations;

namespace BE4_LUUTHANHQUAN.Models.ViewModel
{
    public class AdminUserVM
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [StringLength(255, MinimumLength = 3)]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; }

        [DataType(DataType.Password)]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 50 ký tự")]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Xác nhận mật khẩu không khớp")]
        [Display(Name = "Xác nhận mật khẩu")]
        public string ConfirmPassword { get; set; }
    }
}
