using System.Linq;
using System.Data.Entity;
using System.Web.Mvc;
using BE4_LUUTHANHQUAN.Models;

namespace BE4_LUUTHANHQUAN.Areas.Admin.Controllers
{
    [Authorize(Roles = "A,Admin")]
    public class CustomersController : Controller
    {
        private readonly MyStoreEntities db = new MyStoreEntities();

        // Danh sách khách hàng: chỉ xem, không thêm/sửa/xóa theo checklist.
        public ActionResult Index(string keyword)
        {
            var query = db.Customers.Include("User").AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(c => c.CustomerName.Contains(keyword)
                    || c.CustomerPhone.Contains(keyword)
                    || c.CustomerEmail.Contains(keyword)
                    || c.Username.Contains(keyword));
            }
            ViewBag.Keyword = keyword;
            return View(query.OrderBy(c => c.CustomerName).ToList());
        }

        public ActionResult Details(int? id)
        {
            if (!id.HasValue) return new HttpStatusCodeResult(400);
            var customer = db.Customers
                .Include("Orders.OrderDetails.Product")
                .FirstOrDefault(c => c.CustomerID == id.Value);
            if (customer == null) return HttpNotFound();
            return View(customer);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
