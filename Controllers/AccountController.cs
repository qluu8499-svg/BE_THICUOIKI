using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using BE4_LUUTHANHQUAN.Models;
using BE4_LUUTHANHQUAN.Models.ViewModel;

namespace BE4_LUUTHANHQUAN.Controllers
{
    public class AccountController : Controller
    {
        private readonly MyStoreEntities db = new MyStoreEntities();

        public ActionResult Register() { return View("~/Views/Home/Register.cshtml"); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Register(RegisterVM model)
        {
            if (!ModelState.IsValid) return View("~/Views/Home/Register.cshtml", model);
            model.Username = (model.Username ?? String.Empty).Trim();
            if (db.Users.Any(u => u.Username == model.Username))
            {
                ModelState.AddModelError("Username", "Tên đăng nhập này đã tồn tại!");
                return View("~/Views/Home/Register.cshtml", model);
            }
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    db.Users.Add(new User { Username = model.Username.Trim(), Password = HashPassword(model.Password), UserRole = "C" });
                    db.Customers.Add(new Customer { CustomerName = model.CustomerName, CustomerEmail = model.CustomerEmail, CustomerPhone = model.CustomerPhone, CustomerAddress = model.CustomerAddress, Username = model.Username.Trim() });
                    db.SaveChanges(); tx.Commit();
                }
                catch { tx.Rollback(); ModelState.AddModelError("", "Không thể tạo tài khoản. Vui lòng thử lại."); return View("~/Views/Home/Register.cshtml", model); }
            }
            TempData["Message"] = "Đăng ký thành công. Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        public ActionResult Login(string returnUrl) { return View(new LoginVM { ReturnUrl = returnUrl }); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Login(LoginVM model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = db.Users.SingleOrDefault(u => u.Username == model.Username);
            if (user == null || !VerifyPassword(model.Password, user.Password))
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng."); return View(model);
            }
            // Nâng cấp mật khẩu cũ đang lưu dạng văn bản thuần sau lần đăng nhập hợp lệ.
            if (!user.Password.Trim().StartsWith("P$")) { user.Password = HashPassword(model.Password); db.SaveChanges(); }
            var ticket = new FormsAuthenticationTicket(1, user.Username, DateTime.Now, DateTime.Now.AddHours(8), model.RememberMe, user.UserRole ?? "Customer");
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket)) { HttpOnly = true };
            if (model.RememberMe) cookie.Expires = ticket.Expiration;
            Response.Cookies.Add(cookie);
            if (!String.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)) return Redirect(model.ReturnUrl);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Logout() { FormsAuthentication.SignOut(); Session.Clear(); return RedirectToAction("Index", "Home"); }

        [Authorize]
        public ActionResult ProfileInfo()
        {
            var customer = db.Customers.FirstOrDefault(c => c.Username == User.Identity.Name);
            if (customer == null) return HttpNotFound();
            return View(new ProfileVM
            {
                CustomerName = customer.CustomerName,
                CustomerPhone = customer.CustomerPhone,
                CustomerEmail = customer.CustomerEmail,
                CustomerAddress = customer.CustomerAddress
            });
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(ProfileVM model)
        {
            // Chỉ kiểm tra các trường thông tin cá nhân; mật khẩu được xử lý riêng.
            ModelState.Remove("CurrentPassword");
            ModelState.Remove("NewPassword");
            ModelState.Remove("ConfirmNewPassword");
            if (!ModelState.IsValid) return View("ProfileInfo", model);

            var customer = db.Customers.FirstOrDefault(c => c.Username == User.Identity.Name);
            if (customer == null) return HttpNotFound();
            customer.CustomerName = model.CustomerName.Trim();
            customer.CustomerPhone = model.CustomerPhone.Trim();
            customer.CustomerEmail = String.IsNullOrWhiteSpace(model.CustomerEmail) ? null : model.CustomerEmail.Trim();
            customer.CustomerAddress = model.CustomerAddress;
            db.SaveChanges();
            TempData["Message"] = "Đã cập nhật thông tin cá nhân.";
            return RedirectToAction("ProfileInfo");
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ProfileVM model)
        {
            if (String.IsNullOrWhiteSpace(model.CurrentPassword) ||
                String.IsNullOrWhiteSpace(model.NewPassword) ||
                String.IsNullOrWhiteSpace(model.ConfirmNewPassword))
            {
                TempData["PasswordError"] = "Vui lòng nhập đầy đủ thông tin đổi mật khẩu.";
                return RedirectToAction("ProfileInfo");
            }
            if (model.NewPassword.Length < 6 || model.NewPassword.Length > 50 ||
                model.NewPassword != model.ConfirmNewPassword)
            {
                TempData["PasswordError"] = "Mật khẩu mới phải từ 6 đến 50 ký tự và phần xác nhận phải trùng khớp.";
                return RedirectToAction("ProfileInfo");
            }
            var user = db.Users.SingleOrDefault(u => u.Username == User.Identity.Name);
            if (user == null) return HttpNotFound();
            if (!VerifyPassword(model.CurrentPassword, user.Password))
            {
                TempData["PasswordError"] = "Mật khẩu hiện tại không chính xác.";
                return RedirectToAction("ProfileInfo");
            }
            user.Password = HashPassword(model.NewPassword);
            db.SaveChanges();
            TempData["PasswordMessage"] = "Đổi mật khẩu thành công.";
            return RedirectToAction("ProfileInfo");
        }

        private static string HashPassword(string password)
        {
            var salt = new byte[8]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            using (var derive = new Rfc2898DeriveBytes(password, salt, 100000))
                return "P$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(derive.GetBytes(24));
        }
        private static bool VerifyPassword(string password, string stored)
        {
            if (String.IsNullOrEmpty(stored)) return false;
            stored = stored.Trim();
            if (!stored.StartsWith("P$")) return String.Equals(password, stored, StringComparison.Ordinal);
            var parts = stored.Split('$'); if (parts.Length != 3) return false;
            try { using (var derive = new Rfc2898DeriveBytes(password, Convert.FromBase64String(parts[1]), 100000)) return FixedEquals(derive.GetBytes(24), Convert.FromBase64String(parts[2])); }
            catch { return false; }
        }
        private static bool FixedEquals(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; int diff = 0; for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i]; return diff == 0; }
        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}
