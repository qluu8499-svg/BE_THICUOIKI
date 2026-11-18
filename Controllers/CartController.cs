using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Web.Mvc;
using BE4_LUUTHANHQUAN.Models;
using BE4_LUUTHANHQUAN.Models.ViewModel;

namespace BE4_LUUTHANHQUAN.Controllers
{
    public class CartController : Controller
    {
        private readonly MyStoreEntities db = new MyStoreEntities();
        private List<CartItemVM> Cart { get { var cart = Session["ShoppingCart"] as List<CartItemVM>; if (cart == null) { cart = new List<CartItemVM>(); Session["ShoppingCart"] = cart; } return cart; } }

        public ActionResult Index() { return View(Cart); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddToCart(int id, int quantity = 1)
        {
            if (quantity < 1) quantity = 1;
            var product = db.Products.Find(id); if (product == null) return HttpNotFound();
            quantity = Math.Min(quantity, 99);
            var item = Cart.FirstOrDefault(x => x.ProductID == id);
            if (item == null) Cart.Add(new CartItemVM { ProductID = id, ProductName = product.ProductName, ProductImage = product.ProductImage, UnitPrice = product.ProductPrice, Quantity = quantity });
            else item.Quantity = Math.Min(99, Math.Max(1, item.Quantity) + quantity);
            TempData["Message"] = "Đã thêm sản phẩm vào giỏ hàng.";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Update(int id, int quantity)
        {
            var item = Cart.FirstOrDefault(x => x.ProductID == id);
            if (item != null) { if (quantity <= 0) Cart.Remove(item); else item.Quantity = Math.Min(quantity, 99); }
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Remove(int id) { var item = Cart.FirstOrDefault(x => x.ProductID == id); if (item != null) Cart.Remove(item); return RedirectToAction("Index"); }

        [Authorize]
        public ActionResult Checkout()
        {
            if (!Cart.Any()) return RedirectToAction("Index");
            var customer = db.Customers.FirstOrDefault(c => c.Username == User.Identity.Name);
            if (customer == null) return RedirectToAction("Login", "Account");
            return View(new CheckoutVM { CustomerName = customer.CustomerName, CustomerPhone = customer.CustomerPhone, AddressDelivery = customer.CustomerAddress, PaymentMethod = "COD", ShippingMethod = "Standard", ShippingFee = 25000m });
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public ActionResult Checkout(CheckoutVM model)
        {
            if (!Cart.Any()) return RedirectToAction("Index");
            if (!ModelState.IsValid) return View(model);
            if (model.ShippingMethod != "Standard" && model.ShippingMethod != "Express")
            {
                ModelState.AddModelError("ShippingMethod", "Phương thức giao hàng không hợp lệ.");
                return View(model);
            }
            model.ShippingFee = model.ShippingMethod == "Express" ? 40000m : 25000m;
            if (model.PaymentMethod != "COD" && model.PaymentMethod != "PayPal")
            {
                ModelState.AddModelError("PaymentMethod", "Phương thức thanh toán không hợp lệ.");
                return View(model);
            }
            var customer = db.Customers.FirstOrDefault(c => c.Username == User.Identity.Name);
            if (customer == null) return RedirectToAction("Login", "Account");

            if (model.PaymentMethod == "PayPal")
            {
                // PayPalController tao giao dich; don hang chi duoc ghi DB sau khi PayPal capture thanh cong.
                // PayPal quay lai bang request moi, vi vay luu snapshot trong Session.
                Session["PayPalCheckoutSnapshot"] = new CheckoutSnapshot
                {
                    CustomerID = customer.CustomerID,
                    CustomerName = model.CustomerName,
                    CustomerPhone = model.CustomerPhone,
                    AddressDelivery = model.AddressDelivery,
                    ShippingMethod = model.ShippingMethod,
                    ShippingFee = model.ShippingFee
                };
                return RedirectToAction("PaymentWithPaypal", "Paypal");
            }

            return CreateCodOrder(model, customer);
        }

        private ActionResult CreateCodOrder(CheckoutVM model, Customer customer)
        {
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var orderProducts = Cart.Select(item => new { Item = item, Product = db.Products.Find(item.ProductID) }).ToList();
                    if (orderProducts.Any(x => x.Product == null)) throw new InvalidOperationException("Một sản phẩm trong giỏ hàng không còn tồn tại.");
                    foreach (var line in orderProducts) line.Item.Quantity = Math.Min(99, Math.Max(1, line.Item.Quantity));
                    var order = new Order { CustomerID = customer.CustomerID, OrderDate = DateTime.Now, AddressDelivery = model.AddressDelivery, PaymentStatus = "Chờ xác nhận", TotalAmount = orderProducts.Sum(x => x.Product.ProductPrice * x.Item.Quantity) + model.ShippingFee, ShippingMethod = model.ShippingMethod, ShippingFee = model.ShippingFee };
                    db.Orders.Add(order); db.SaveChanges();
                    foreach (var line in orderProducts)
                        db.OrderDetails.Add(new OrderDetail { OrderID = order.OrderID, ProductID = line.Item.ProductID, Quantity = line.Item.Quantity, UnitPrice = line.Product.ProductPrice });
                    customer.CustomerName = model.CustomerName; customer.CustomerPhone = model.CustomerPhone; customer.CustomerAddress = model.AddressDelivery;
                    db.SaveChanges(); tx.Commit(); Session.Remove("ShoppingCart");
                    return RedirectToAction("OrderSuccess", new { id = order.OrderID });
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    // Hiển thị nguyên nhân gốc trong môi trường phát triển để dễ xác định lỗi cấu hình DB.
                    var error = ex.GetBaseException().Message;
                    ModelState.AddModelError("", "Đặt hàng COD chưa thành công: " + error);
                    return View("Checkout", model);
                }
            }
        }

        [Authorize]
        public ActionResult OrderSuccess(int id)
        {
            var customer = db.Customers.FirstOrDefault(c => c.Username == User.Identity.Name);
            if (customer == null) return HttpNotFound();
            var order = db.Orders.Include("OrderDetails.Product")
                .FirstOrDefault(o => o.OrderID == id && o.CustomerID == customer.CustomerID);
            if (order == null) return HttpNotFound();
            return View(order);
        }

        [Serializable]
        public class CheckoutSnapshot
        {
            public int CustomerID { get; set; }
            public string CustomerName { get; set; }
            public string CustomerPhone { get; set; }
            public string AddressDelivery { get; set; }
            public string ShippingMethod { get; set; }
            public decimal ShippingFee { get; set; }
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}
