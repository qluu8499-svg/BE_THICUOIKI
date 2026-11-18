using System;
using System.Linq;
using System.Data.Entity;
using System.Web.Mvc;
using BE4_LUUTHANHQUAN.Models;
using BE4_LUUTHANHQUAN.Models.ViewModel;
using System.Collections.Generic;

namespace BE4_LUUTHANHQUAN.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly MyStoreEntities db = new MyStoreEntities();
        private static readonly string[] Statuses = { "Chờ thanh toán", "Đang xử lý", "Đang vận chuyển", "Đã giao", "Đã hủy" };
        private Customer CurrentCustomer() { return db.Customers.FirstOrDefault(c => c.Username == User.Identity.Name); }
        private IQueryable<Order> OrdersOf(int customerId) { return db.Orders.Include("OrderDetails.Product").Where(o => o.CustomerID == customerId); }

        public ActionResult Index(string status, string keyword, bool showMore = false)
        {
            var customer = CurrentCustomer();
            if (customer == null) return RedirectToAction("Login", "Account");
            var hasAnyOrders = OrdersOf(customer.CustomerID).Any();
            var query = OrdersOf(customer.CustomerID);
            if (!String.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                int orderId;
                if (Int32.TryParse(keyword, out orderId))
                    query = query.Where(o => o.OrderID == orderId || o.OrderDetails.Any(d => d.Product != null && d.Product.ProductName.Contains(keyword)));
                else query = query.Where(o => o.OrderDetails.Any(d => d.Product != null && d.Product.ProductName.Contains(keyword)));
            }
            var statusCounts = new Dictionary<string, int>();
            if (!String.IsNullOrWhiteSpace(status))
            {
                if (status == "Chờ thanh toán") query = query.Where(o => o.PaymentStatus == "Chờ thanh toán" || o.PaymentStatus == "Chưa thanh toán" || o.PaymentStatus == "Chờ xác nhận");
                else if (status == "Đang vận chuyển") query = query.Where(o => o.PaymentStatus == "Đang vận chuyển" || o.PaymentStatus == "Đang giao");
                else query = query.Where(o => o.PaymentStatus == status);
                statusCounts[status] = query.Count();
                query = query.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderID).Take(showMore ? 50 : 2);
            }
            else
            {
                var grouped = new List<Order>();
                foreach (var s in Statuses)
                {
                    var groupQuery = OrdersOf(customer.CustomerID);
                    if (!String.IsNullOrWhiteSpace(keyword))
                    {
                        int orderId;
                        if (Int32.TryParse(keyword, out orderId))
                            groupQuery = groupQuery.Where(o => o.OrderID == orderId || o.OrderDetails.Any(d => d.Product != null && d.Product.ProductName.Contains(keyword)));
                        else groupQuery = groupQuery.Where(o => o.OrderDetails.Any(d => d.Product != null && d.Product.ProductName.Contains(keyword)));
                    }
                    if (s == "Chờ thanh toán") groupQuery = groupQuery.Where(o => o.PaymentStatus == "Chờ thanh toán" || o.PaymentStatus == "Chưa thanh toán" || o.PaymentStatus == "Chờ xác nhận");
                    else if (s == "Đang vận chuyển") groupQuery = groupQuery.Where(o => o.PaymentStatus == "Đang vận chuyển" || o.PaymentStatus == "Đang giao");
                    else groupQuery = groupQuery.Where(o => o.PaymentStatus == s);
                    statusCounts[s] = groupQuery.Count();
                    grouped.AddRange(groupQuery.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderID).Take(50).ToList());
                }
                query = grouped.AsQueryable();
            }
            ViewBag.Status = status;
            ViewBag.Keyword = keyword;
            ViewBag.ShowMore = showMore;
            ViewBag.HasAnyOrders = hasAnyOrders;
            ViewBag.StatusCounts = statusCounts;
            return View(query.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderID).ToList());
        }

        public ActionResult Details(int id)
        {
            var customer = CurrentCustomer();
            if (customer == null) return RedirectToAction("Login", "Account");
            var order = db.Orders.Include("OrderDetails.Product").FirstOrDefault(o => o.OrderID == id && o.CustomerID == customer.CustomerID);
            return order == null ? (ActionResult)HttpNotFound() : View(order);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Reorder(int id)
        {
            var customer = CurrentCustomer();
            if (customer == null) return RedirectToAction("Login", "Account");
            var order = db.Orders.Include("OrderDetails.Product").FirstOrDefault(o => o.OrderID == id && o.CustomerID == customer.CustomerID);
            if (order == null) return HttpNotFound();
            var cart = Session["ShoppingCart"] as List<CartItemVM>;
            if (cart == null) cart = new List<CartItemVM>();
            foreach (var productId in order.OrderDetails.Select(line => line.ProductID).Distinct())
            {
                var product = db.Products.Find(productId);
                if (product == null) continue;
                var item = cart.FirstOrDefault(x => x.ProductID == product.ProductID);
                if (item == null) cart.Add(new CartItemVM { ProductID = product.ProductID, ProductName = product.ProductName, ProductImage = product.ProductImage, UnitPrice = product.ProductPrice, Quantity = 1 });
                else item.Quantity = Math.Min(99, item.Quantity + 1);
            }
            Session["ShoppingCart"] = cart;
            TempData["Message"] = "Đã thêm mỗi sản phẩm còn tồn tại vào giỏ hàng với số lượng 1.";
            return RedirectToAction("Index", "Cart");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            var customer = CurrentCustomer();
            if (customer == null) return RedirectToAction("Login", "Account");
            var order = db.Orders.FirstOrDefault(o => o.OrderID == id && o.CustomerID == customer.CustomerID);
            if (order == null) return HttpNotFound();
            if (order.PaymentStatus == "Chờ xác nhận" || order.PaymentStatus == "Chưa thanh toán" || order.PaymentStatus == "Chờ thanh toán")
            {
                order.PaymentStatus = "Đã hủy"; db.SaveChanges();
                TempData["OrderMessage"] = "Đã hủy đơn hàng #" + id + ".";
            }
            else TempData["OrderMessage"] = "Đơn hàng đang được xử lý nên không thể tự hủy.";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}
