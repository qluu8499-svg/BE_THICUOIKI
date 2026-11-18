using System;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Mvc;
using BE4_LUUTHANHQUAN.Models;
using BE4_LUUTHANHQUAN.Models.ViewModel;

namespace BE4_LUUTHANHQUAN.Areas.Admin.Controllers
{
    [Authorize(Roles = "A,Admin")]
    public class UsersController : Controller
    {
        private readonly MyStoreEntities db = new MyStoreEntities();

        private IQueryable<User> AdminUsers()
        {
            return db.Users.Where(u => u.UserRole == "A" || u.UserRole == "Admin");
        }

        public ActionResult Index(string keyword)
        {
            var query = AdminUsers();
            if (!String.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(u => u.Username.Contains(keyword));
            }
            ViewBag.Keyword = keyword;
            return View(query.OrderBy(u => u.Username).ToList());
        }

        public ActionResult Create() { return View(new AdminUserVM()); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(AdminUserVM model)
        {
            if (String.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError("Password", "Vui lòng nhập mật khẩu.");
            if (db.Users.Any(u => u.Username == model.Username))
                ModelState.AddModelError("Username", "Tên đăng nhập đã tồn tại.");
            if (!ModelState.IsValid) return View(model);

            db.Users.Add(new User
            {
                Username = model.Username.Trim(),
                Password = HashPassword(model.Password),
                UserRole = "A"
            });
            db.SaveChanges();
            TempData["AdminUserMessage"] = "Đã tạo tài khoản quản trị.";
            return RedirectToAction("Index");
        }

        public ActionResult Edit(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return new HttpStatusCodeResult(400);
            var user = AdminUsers().FirstOrDefault(u => u.Username == id);
            if (user == null) return HttpNotFound();
            return View(new AdminUserVM { Username = user.Username });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(AdminUserVM model)
        {
            ModelState.Remove("Password");
            ModelState.Remove("ConfirmPassword");
            var user = AdminUsers().FirstOrDefault(u => u.Username == model.Username);
            if (user == null) return HttpNotFound();

            if (!String.IsNullOrWhiteSpace(model.Password))
            {
                if (model.Password.Length < 6 || model.Password.Length > 50 ||
                    model.Password != model.ConfirmPassword)
                {
                    ModelState.AddModelError("Password", "Mật khẩu mới phải từ 6 đến 50 ký tự và xác nhận phải trùng khớp.");
                }
            }
            if (!ModelState.IsValid) return View(model);

            if (!String.IsNullOrWhiteSpace(model.Password))
                user.Password = HashPassword(model.Password);
            db.SaveChanges();
            TempData["AdminUserMessage"] = "Đã cập nhật tài khoản quản trị.";
            return RedirectToAction("Index");
        }

        public ActionResult Delete(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return new HttpStatusCodeResult(400);
            var user = AdminUsers().FirstOrDefault(u => u.Username == id);
            if (user == null) return HttpNotFound();
            return View(user);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return new HttpStatusCodeResult(400);
            if (String.Equals(id, User.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["AdminUserError"] = "Không thể xóa tài khoản quản trị đang đăng nhập.";
                return RedirectToAction("Index");
            }
            var user = AdminUsers().FirstOrDefault(u => u.Username == id);
            if (user == null) return HttpNotFound();
            if (db.Customers.Any(c => c.Username == id))
            {
                TempData["AdminUserError"] = "Tài khoản này đang được liên kết với hồ sơ khách hàng nên không thể xóa.";
                return RedirectToAction("Index");
            }
            if (AdminUsers().Count() <= 1)
            {
                TempData["AdminUserError"] = "Phải giữ lại ít nhất một tài khoản quản trị.";
                return RedirectToAction("Index");
            }
            db.Users.Remove(user);
            db.SaveChanges();
            TempData["AdminUserMessage"] = "Đã xóa tài khoản quản trị.";
            return RedirectToAction("Index");
        }

        private static string HashPassword(string password)
        {
            var salt = new byte[8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            using (var derive = new Rfc2898DeriveBytes(password, salt, 100000))
                return "P$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(derive.GetBytes(24));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
