using System;
using System.Linq;
using System.Data.Entity;
using System.Web.Mvc;
using BE4_LUUTHANHQUAN.Models;
using PagedList;

namespace BE4_LUUTHANHQUAN.Areas.Admin.Controllers
{
    [Authorize(Roles = "A,Admin")]
    public class OrdersController : Controller
    {
        private readonly MyStoreEntities db = new MyStoreEntities();

        // Giữ keyword/status để các liên kết cũ vẫn hoạt động; bổ sung bộ lọc theo đề.
        public ActionResult Index(string customerName, string paymentStatus, DateTime? fromDate,
            DateTime? toDate, int? page, string keyword, string status, string sortOrder)
        {
            customerName = String.IsNullOrWhiteSpace(customerName) ? keyword : customerName;
            paymentStatus = String.IsNullOrWhiteSpace(paymentStatus) ? status : paymentStatus;
            var query = db.Orders.Include("Customer").Include("OrderDetails.Product").AsQueryable();
            if (!String.IsNullOrWhiteSpace(customerName))
            {
                customerName = customerName.Trim();
                // Tim tren thong tin khach hang va san pham trong don.
                // Cac dieu kien duoc dich xuong SQL boi Entity Framework.
                query = query.Where(o =>
                    (o.Customer != null &&
                        ((o.Customer.CustomerName != null && o.Customer.CustomerName.Contains(customerName)) ||
                         (o.Customer.CustomerPhone != null && o.Customer.CustomerPhone.Contains(customerName)) ||
                         (o.Customer.CustomerEmail != null && o.Customer.CustomerEmail.Contains(customerName)) ||
                         (o.Customer.Username != null && o.Customer.Username.Contains(customerName)))) ||
                    o.OrderDetails.Any(d => d.Product != null &&
                        d.Product.ProductName != null && d.Product.ProductName.Contains(customerName)));
            }
            if (!String.IsNullOrWhiteSpace(paymentStatus))
            {
                var selected = paymentStatus.Trim();
                if (selected == "Chờ thanh toán")
                    query = query.Where(o => o.PaymentStatus == "Chờ thanh toán" || o.PaymentStatus == "Chưa thanh toán" || o.PaymentStatus == "Chờ xác nhận");
                else if (selected == "Đang vận chuyển")
                    query = query.Where(o => o.PaymentStatus == "Đang vận chuyển" || o.PaymentStatus == "Đang giao");
                else query = query.Where(o => o.PaymentStatus == selected);
            }
            if (fromDate.HasValue) query = query.Where(o => o.OrderDate >= fromDate.Value);
            if (toDate.HasValue)
            {
                var toDateExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(o => o.OrderDate < toDateExclusive);
            }

            ViewBag.CustomerName = customerName;
            ViewBag.PaymentStatus = paymentStatus;
            ViewBag.FromDate = fromDate.HasValue ? fromDate.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.ToDate = toDate.HasValue ? toDate.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.SortOrder = sortOrder;
            switch (sortOrder)
            {
                case "date_asc": query = query.OrderBy(o => o.OrderDate).ThenBy(o => o.OrderID); break;
                case "amount_desc": query = query.OrderByDescending(o => o.TotalAmount).ThenByDescending(o => o.OrderDate); break;
                case "amount_asc": query = query.OrderBy(o => o.TotalAmount).ThenByDescending(o => o.OrderDate); break;
                case "customer_asc": query = query.OrderBy(o => o.Customer.CustomerName).ThenByDescending(o => o.OrderDate); break;
                default: query = query.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderID); break;
            }
            return View(query.ToPagedList(Math.Max(1, page ?? 1), 15));
        }

        public ActionResult Details(int id)
        {
            var order = db.Orders.Include("Customer").Include("OrderDetails.Product").FirstOrDefault(o => o.OrderID == id);
            return order == null ? (ActionResult)HttpNotFound() : View(order);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int id, string status)
        {
            var allowed = new[] { "Chờ thanh toán", "Chờ xác nhận", "Chưa thanh toán", "Đang xử lý", "Đang vận chuyển", "Đang giao", "Đã giao", "Đã hủy" };
            if (String.IsNullOrWhiteSpace(status) || !allowed.Contains(status)) return new HttpStatusCodeResult(400);
            var order = db.Orders.Find(id);
            if (order == null) return HttpNotFound();
            order.PaymentStatus = status;
            db.SaveChanges();
            TempData["OrderMessage"] = "Đã cập nhật trạng thái đơn hàng #" + id + ".";
            return RedirectToAction("Details", new { id = id });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
