using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BE4_LUUTHANHQUAN.Models.ViewModel
{
    public class CheckoutVM
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [Display(Name = "Người nhận")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Display(Name = "Số điện thoại")]
        public string CustomerPhone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng")]
        [Display(Name = "Địa chỉ giao hàng")]
        public string AddressDelivery { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức giao hàng")]
        public string ShippingMethod { get; set; }

        public decimal ShippingFee { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        [Display(Name = "Phương thức thanh toán")]
        public string PaymentMethod { get; set; }

        // Sửa CartItem thành CartItemVM
        public List<CartItemVM> CartItems { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
