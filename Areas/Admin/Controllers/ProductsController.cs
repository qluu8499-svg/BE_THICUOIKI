using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.IO;
using BE4_LUUTHANHQUAN.Models;
using BE4_LUUTHANHQUAN.Models.ViewModel;
using PagedList;
using Microsoft.Ajax.Utilities;
namespace BE4_LUUTHANHQUAN.Areas.Admin.Controllers
   
{
    [Authorize(Roles = "A,Admin")]
    public class ProductsController : Controller
    {
        private MyStoreEntities db = new MyStoreEntities();

        // GET: Admin/Products
        public ActionResult Index(string searchTerm, decimal? minPrice,
            decimal? maxPrice, string sortOrder, int? page)
        {
            var model = new ProductSearchVM();
            var products = db.Products.AsQueryable();
            // Tìm kiếm sản phẩm dựa trên từ khóa
            if (!string.IsNullOrEmpty(searchTerm))
            {
                model.SearchTerm = searchTerm;
                products = products.Where(p => p.ProductName.Contains(searchTerm) ||
                                               p.ProductDescription.Contains(searchTerm) ||
                                               p.Category.CategoryName.Contains(searchTerm));
            }
            // Tìm kiếm sản phẩm dựa trên giá tối thiểu
            if (minPrice.HasValue)
            {
                model.MinPrice = minPrice.Value;
                products = products.Where(p => p.ProductPrice >= minPrice.Value);
            }
            // Tìm kiếm sản phẩm dựa trên giá tối đa
            if (maxPrice.HasValue)
            {
                model.MaxPrice = maxPrice.Value;
                products = products.Where(p => p.ProductPrice <= maxPrice.Value);
            }
            // Áp dụng sắp xếp dựa trên lựa chọn của người dùng
            switch (sortOrder)
            {
                case "name_asc":
                    products = products.OrderBy(p => p.ProductName);
                    break;
                case "name_desc":
                    products = products.OrderByDescending(p => p.ProductName);
                    break;
                case "price_asc":
                    products = products.OrderBy(p => p.ProductPrice);
                    break;
                case "price_desc":
                    products = products.OrderByDescending(p => p.ProductPrice);
                    break;
                default: // Mặc định sắp xếp theo tên
                    products = products.OrderBy(p => p.ProductName);
                    break;
            }
            model.SortOrder = sortOrder;

          
          
            int pageNumber = Math.Max(1, page ?? 1);
            int pageSize = 2; // Số sản phẩm mỗi trang

            // Đóng câu lệnh này, sử dụng ToPagedList để lấy danh sách đã phân trang
            //model.Products = products.ToList();
            model.Products = products.ToPagedList(pageNumber, pageSize);

            return View(model);
        }





        // GET: Admin/Products/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = db.Products.Find(id);
            if (product == null)
            {
                return HttpNotFound();
            }
            return View(product);
        }

        // GET: Admin/Products/Create
        public ActionResult Create()
        {
            ViewBag.CategoryID = new SelectList(db.Categories, "CategoryID", "CategoryName");
            return View();
        }

        // POST: Admin/Products/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "ProductID,CategoryID,ProductName,ProductDescription,ProductPrice,ProductImage")] Product product, HttpPostedFileBase imageFile)
        {
            if (imageFile != null && imageFile.ContentLength > 0)
            {
                string uploadedPath = SaveProductImage(imageFile);
                if (uploadedPath == null)
                    ModelState.AddModelError("ProductImage", "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP tối đa 5 MB.");
                else product.ProductImage = uploadedPath;
            }
            if (ModelState.IsValid)
            {
                try
                {
                    db.Products.Add(product);
                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
                {
                    foreach (var validationErrors in dbEx.EntityValidationErrors)
                    {
                        foreach (var validationError in validationErrors.ValidationErrors)
                        {
                            // Đoạn này sẽ ghi nhận chính xác Tên thuộc tính và Lỗi cụ thể là gì
                            System.Diagnostics.Trace.TraceInformation("Property: {0} Error: {1}",
                                                    validationError.PropertyName,
                                                    validationError.ErrorMessage);
                        }
                    }
                    throw; // Bấm tiếp tục chạy để xem log trong Output window của Visual Studio
                }

            }

            ViewBag.CategoryID = new SelectList(db.Categories, "CategoryID", "CategoryName", product.CategoryID);
            return View(product);
        }

        // GET: Admin/Products/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = db.Products.Find(id);
            if (product == null)
            {
                return HttpNotFound();
            }
            ViewBag.CategoryID = new SelectList(db.Categories, "CategoryID", "CategoryName", product.CategoryID);
            return View(product);
        }

        // POST: Admin/Products/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ProductID,CategoryID,ProductName,ProductDescription,ProductPrice,ProductImage")] Product product, HttpPostedFileBase imageFile)
        {
            var existing = db.Products.Find(product.ProductID);
            if (existing == null) return HttpNotFound();

            if (imageFile != null && imageFile.ContentLength > 0)
            {
                string uploadedPath = SaveProductImage(imageFile);
                if (uploadedPath == null)
                    ModelState.AddModelError("ProductImage", "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP tối đa 5 MB.");
                else product.ProductImage = uploadedPath;
            }
            else
            {
                // Không chọn ảnh mới thì giữ nguyên ảnh hiện tại trong database.
                product.ProductImage = existing.ProductImage;
            }

            if (ModelState.IsValid)
            {
                existing.CategoryID = product.CategoryID;
                existing.ProductName = product.ProductName;
                existing.ProductDescription = product.ProductDescription;
                existing.ProductPrice = product.ProductPrice;
                existing.ProductImage = product.ProductImage;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.CategoryID = new SelectList(db.Categories, "CategoryID", "CategoryName", product.CategoryID);
            return View(product);
        }

        // GET: Admin/Products/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = db.Products.Find(id);
            if (product == null)
            {
                return HttpNotFound();
            }
            return View(product);
        }

        // POST: Admin/Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Product product = db.Products.Find(id);
            if (product == null) return HttpNotFound();
            if (product.OrderDetails.Any())
            {
                TempData["DeleteError"] = "Không thể xóa sản phẩm đã phát sinh đơn hàng. Bạn có thể chỉnh sửa hoặc ẩn sản phẩm thay thế.";
                return RedirectToAction("Delete", new { id = id });
            }
            db.Products.Remove(product);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        private string SaveProductImage(HttpPostedFileBase imageFile)
        {
            if (imageFile == null || imageFile.ContentLength <= 0 || imageFile.ContentLength > 5 * 1024 * 1024)
                return null;
            string extension = Path.GetExtension(imageFile.FileName);
            string[] allowed = { ".jpg", ".jpeg", ".png", ".webp" };
            if (String.IsNullOrWhiteSpace(extension) || !allowed.Contains(extension.ToLowerInvariant()))
                return null;

            string folder = Server.MapPath("~/Content/Images/");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string fileName = Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
            imageFile.SaveAs(Path.Combine(folder, fileName));
            return "~/Content/Images/" + fileName;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
