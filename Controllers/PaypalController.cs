using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Data.Entity;
using System.Web.Mvc;
using BE4_LUUTHANHQUAN.Models;
using BE4_LUUTHANHQUAN.Models.ViewModel;
using PayPal.Api;
using Item = PayPal.Api.Item;
using Payer = PayPal.Api.Payer;

namespace BE4_LUUTHANHQUAN.Controllers
{
    [Authorize]
    public class PaypalController : Controller
    {
        private readonly MyStoreEntities db = new MyStoreEntities();
        private const string CheckoutSessionKey = "PayPalCheckoutSnapshot";
        private const string PaymentSessionPrefix = "PayPalPayment_";

        private APIContext GetAPIContext()
        {
            var clientId = ConfigurationManager.AppSettings["PayPalClientId"];
            var clientSecret = ConfigurationManager.AppSettings["PayPalClientSecret"];
            var mode = ConfigurationManager.AppSettings["PayPalMode"];
            if (String.IsNullOrWhiteSpace(clientId) || String.IsNullOrWhiteSpace(clientSecret) ||
                clientId.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) ||
                clientSecret.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
                throw new ConfigurationErrorsException("Chưa cấu hình PayPalClientId/PayPalClientSecret trong Web.config.");

            var config = new Dictionary<string, string>
            {
                { "mode", String.IsNullOrWhiteSpace(mode) ? "sandbox" : mode },
                { "clientId", clientId },
                { "clientSecret", clientSecret }
            };
            var accessToken = new OAuthTokenCredential(config).GetAccessToken();
            return new APIContext(accessToken) { Config = config };
        }

        private Payment CreatePayment(APIContext apiContext, string returnUrl, CheckoutVM model)
        {
            var items = new List<Item>();
            foreach (var line in model.CartItems)
            {
                items.Add(new Item
                {
                    name = line.ProductName,
                    currency = "USD",
                    price = line.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture),
                    quantity = line.Quantity.ToString(CultureInfo.InvariantCulture),
                    sku = line.ProductID.ToString(CultureInfo.InvariantCulture)
                });
            }

            var shipping = model.ShippingFee.ToString("0.00", CultureInfo.InvariantCulture);
            var subtotal = (model.TotalAmount - model.ShippingFee).ToString("0.00", CultureInfo.InvariantCulture);
            var total = model.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture);
            var payment = new Payment
            {
                intent = "sale",
                payer = new Payer { payment_method = "paypal" },
                redirect_urls = new RedirectUrls { cancel_url = Url.Action("PaymentCancelled", "Paypal", null, Request.Url.Scheme), return_url = returnUrl },
                transactions = new List<Transaction>
                {
                    new Transaction
                    {
                        description = "Thanh toan don hang BE4_LUUTHANHQUAN",
                        invoice_number = Guid.NewGuid().ToString("N"),
                        item_list = new ItemList { items = items },
                        amount = new Amount
                        {
                            currency = "USD", total = total,
                            details = new Details { subtotal = subtotal, tax = "0", shipping = shipping }
                        }
                    }
                }
            };
            return payment.Create(apiContext);
        }

        [HttpGet]
        public ActionResult PaymentWithPaypal()
        {
            // PayPal quay lai cung guid va PayerID; khong phu thuoc model binding qua redirect.
            var payerId = Request.QueryString["PayerID"];
            var guid = Request.QueryString["guid"];

            if (String.IsNullOrWhiteSpace(payerId))
            {
                var snapshot = Session[CheckoutSessionKey] as CartController.CheckoutSnapshot;
                var cart = Session["ShoppingCart"] as List<CartItemVM>;
                if (snapshot == null || cart == null || cart.Count == 0)
                    return RedirectToAction("Checkout", "Cart");

                var customer = db.Customers.FirstOrDefault(c => c.CustomerID == snapshot.CustomerID && c.Username == User.Identity.Name);
                if (customer == null) return RedirectToAction("Login", "Account");

                var lines = cart.Select(x => new { Item = x, Product = db.Products.Find(x.ProductID) }).ToList();
                if (lines.Any(x => x.Product == null))
                {
                    TempData["Message"] = "Một sản phẩm trong giỏ hàng không còn tồn tại. Vui lòng kiểm tra lại giỏ hàng.";
                    return RedirectToAction("Index", "Cart");
                }
                var checkout = new CheckoutVM
                {
                    CustomerName = snapshot.CustomerName,
                    CustomerPhone = snapshot.CustomerPhone,
                    AddressDelivery = snapshot.AddressDelivery,
                    ShippingMethod = snapshot.ShippingMethod,
                    ShippingFee = snapshot.ShippingFee,
                    PaymentMethod = "PayPal",
                    CartItems = lines.Select(x => new CartItemVM
                    {
                        ProductID = x.Product.ProductID,
                        ProductName = x.Product.ProductName,
                        UnitPrice = x.Product.ProductPrice,
                        Quantity = Math.Min(99, Math.Max(1, x.Item.Quantity))
                    }).ToList()
                };
                checkout.TotalAmount = checkout.CartItems.Sum(x => x.UnitPrice * x.Quantity) + checkout.ShippingFee;

                try
                {
                    var api = GetAPIContext();
                    var state = Guid.NewGuid().ToString("N");
                    var callback = Url.Action("PaymentWithPaypal", "Paypal", new { guid = state }, Request.Url.Scheme);
                    var created = CreatePayment(api, callback, checkout);
                    var approval = created.links.FirstOrDefault(x => String.Equals(x.rel, "approval_url", StringComparison.OrdinalIgnoreCase));
                    if (approval == null || String.IsNullOrWhiteSpace(approval.href))
                        throw new InvalidOperationException("PayPal không trả về approval_url.");

                    Session[PaymentSessionPrefix + state] = created.id;
                    return Redirect(approval.href);
                }
                catch (Exception)
                {
                    TempData["Message"] = "Không thể khởi tạo thanh toán PayPal. Vui lòng kiểm tra cấu hình PayPal và thử lại.";
                    return RedirectToAction("Checkout", "Cart");
                }
            }

            if (String.IsNullOrWhiteSpace(guid)) return View("FailureView");
            var paymentId = Session[PaymentSessionPrefix + guid] as string;
            var checkoutSnapshot = Session[CheckoutSessionKey] as CartController.CheckoutSnapshot;
            var currentCart = Session["ShoppingCart"] as List<CartItemVM>;
            if (String.IsNullOrWhiteSpace(paymentId) || checkoutSnapshot == null || currentCart == null || currentCart.Count == 0)
                return View("FailureView");

            try
            {
                var executed = new Payment { id = paymentId }.Execute(GetAPIContext(), new PaymentExecution { payer_id = payerId });
                if (executed == null || !String.Equals(executed.state, "approved", StringComparison.OrdinalIgnoreCase))
                    return View("FailureView");

                var customer = db.Customers.FirstOrDefault(c => c.CustomerID == checkoutSnapshot.CustomerID && c.Username == User.Identity.Name);
                if (customer == null) return View("FailureView");

                using (var tx = db.Database.BeginTransaction())
                {
                    var lines = currentCart.Select(x => new { Item = x, Product = db.Products.Find(x.ProductID) }).ToList();
                    if (lines.Any(x => x.Product == null)) throw new InvalidOperationException("Cart contains a missing product.");
                    var order = new BE4_LUUTHANHQUAN.Models.Order
                    {
                        CustomerID = customer.CustomerID,
                        OrderDate = DateTime.Now,
                        AddressDelivery = checkoutSnapshot.AddressDelivery,
                        ShippingMethod = checkoutSnapshot.ShippingMethod,
                        ShippingFee = checkoutSnapshot.ShippingFee,
                        PaymentStatus = "Chờ xác nhận",
                        TotalAmount = lines.Sum(x => x.Product.ProductPrice * Math.Min(99, Math.Max(1, x.Item.Quantity))) + checkoutSnapshot.ShippingFee
                    };
                    db.Orders.Add(order);
                    db.SaveChanges();
                    foreach (var line in lines)
                        db.OrderDetails.Add(new OrderDetail
                        {
                            OrderID = order.OrderID,
                            ProductID = line.Product.ProductID,
                            Quantity = Math.Min(99, Math.Max(1, line.Item.Quantity)),
                            UnitPrice = line.Product.ProductPrice
                        });
                    customer.CustomerName = checkoutSnapshot.CustomerName;
                    customer.CustomerPhone = checkoutSnapshot.CustomerPhone;
                    customer.CustomerAddress = checkoutSnapshot.AddressDelivery;
                    db.SaveChanges();
                    tx.Commit();

                    Session.Remove("ShoppingCart");
                    Session.Remove(CheckoutSessionKey);
                    Session.Remove(PaymentSessionPrefix + guid);
                    return RedirectToAction("OrderSuccess", "Cart", new { id = order.OrderID });
                }
            }
            catch (Exception)
            {
                return View("FailureView");
            }
        }

        [HttpGet]
        public ActionResult PaymentCancelled()
        {
            TempData["Message"] = "Bạn đã hủy thanh toán PayPal. Giỏ hàng vẫn được giữ nguyên.";
            return RedirectToAction("Checkout", "Cart");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
