using System.ComponentModel.DataAnnotations;

namespace BE4_LUUTHANHQUAN.Models
{
    [MetadataType(typeof(CategoryMetadata))]
    public partial class Category { }

    public class CategoryMetadata
    {
        [Required(ErrorMessage = "Vui lòng nhập tên danh mục")]
        [StringLength(100, ErrorMessage = "Tên danh mục không vượt quá 100 ký tự")]
        public string CategoryName { get; set; }
    }

    [MetadataType(typeof(ProductMetadata))]
    public partial class Product { }

    public class ProductMetadata
    {
        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        public int CategoryID { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
        [StringLength(200, ErrorMessage = "Tên sản phẩm không vượt quá 200 ký tự")]
        public string ProductName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mô tả sản phẩm")]
        [StringLength(4000, ErrorMessage = "Mô tả sản phẩm không vượt quá 4000 ký tự")]
        public string ProductDescription { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá")]
        [Range(typeof(decimal), "0.01", "9999999999999999", ErrorMessage = "Giá sản phẩm phải lớn hơn 0")]
        public decimal ProductPrice { get; set; }
    }

    [MetadataType(typeof(CustomerMetadata))]
    public partial class Customer { }

    public class CustomerMetadata
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(150)]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [StringLength(15)]
        public string CustomerPhone { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string CustomerEmail { get; set; }
    }
}
