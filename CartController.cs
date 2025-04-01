using Microsoft.AspNetCore.Mvc;
using WebSiteBanHang.Models;
using WebSiteBanHang.Repositories;
using WebSiteBanHang.Services;
using Microsoft.AspNetCore.Identity;
using WebsiteBanHang.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Text.Encodings.Web;

namespace WebSiteBanHang.Controllers
{
    public class CartController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ShoppingCartService _cartService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public CartController(
            IProductRepository productRepository, 
            ICategoryRepository categoryRepository, 
            ShoppingCartService cartService,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _cartService = cartService;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        public async Task<IActionResult> Index()
        {
            var cartItems = await _cartService.GetCartItemsAsync();
            var total = await _cartService.GetTotalAsync();
            
            var viewModel = new CartViewModel
            {
                CartItems = cartItems,
                CartTotal = total
            };
            
            return View(viewModel);
        }

        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
            {
                return NotFound();
            }

            await _cartService.AddToCartAsync(productId, quantity);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int id, int quantity)
        {
            if (quantity <= 0)
            {
                await _cartService.RemoveFromCartAsync(id);
            }
            else
            {
                await _cartService.UpdateQuantityAsync(id, quantity);
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            await _cartService.RemoveFromCartAsync(id);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Checkout()
        {
            var cartItems = await _cartService.GetCartItemsAsync();
            if (cartItems.Count == 0)
            {
                return RedirectToAction("Index");
            }
            
            var total = await _cartService.GetTotalAsync();
            var categories = await _categoryRepository.GetAllCategoriesAsync();
            
            // Chuẩn bị ViewBag
            ViewBag.Categories = categories;
            
            // Chuẩn bị mô hình dữ liệu
            var subtotal = total;
            var tax = Math.Round(subtotal * 0.1m, 2);  // 10% thuế
            var shippingFee = subtotal >= 500000 ? 0 : 30000; // Miễn phí vận chuyển cho đơn hàng trên 500k
            
            var model = new CheckoutViewModel
            {
                CartItems = cartItems,
                Subtotal = subtotal,
                Tax = tax,
                ShippingFee = shippingFee,
                Total = subtotal + tax + shippingFee
            };
            
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ClearCart()
        {
            await _cartService.ClearCartAsync();
            return RedirectToAction("Index");
        }
        
        [HttpPost]
        public async Task<IActionResult> ProcessCheckout(CheckoutViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Lấy cart items và thông tin người dùng
                var cartItems = await _cartService.GetCartItemsAsync();
                var user = await _userManager.GetUserAsync(User);
                var email = user?.Email;
                
                if (email == null)
                {
                    // Nếu không có email (người dùng chưa đăng nhập), sử dụng email từ form checkout
                    email = model.Email;
                }
                
                // Tạo order ID
                var orderId = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
                
                // Tạo danh sách sản phẩm trong email
                var productList = "";
                foreach (var item in cartItems)
                {
                    productList += $@"
                    <tr>
                        <td style='padding: 12px; border-bottom: 1px solid #e5e7eb;'>
                            {item.Product.Name}
                        </td>
                        <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: center;'>
                            {item.Quantity}
                        </td>
                        <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: right;'>
                            {item.Product.Price.ToString("N0")} đ
                        </td>
                        <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: right;'>
                            {(item.Product.Price * item.Quantity).ToString("N0")} đ
                        </td>
                    </tr>";
                }
                
                // Tạo template email
                string emailTemplate = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='UTF-8'>
                    <title>Xác nhận đơn hàng</title>
                    <style>
                        body {{
                            font-family: Arial, sans-serif;
                            line-height: 1.6;
                            color: #333;
                            margin: 0;
                            padding: 20px;
                        }}
                        .email-container {{
                            max-width: 600px;
                            margin: 0 auto;
                            border: 1px solid #e0e0e0;
                            border-radius: 8px;
                            overflow: hidden;
                        }}
                        .email-header {{
                            background-color: #4f46e5;
                            color: white;
                            padding: 20px;
                            text-align: center;
                        }}
                        .email-body {{
                            padding: 30px;
                            background-color: #ffffff;
                        }}
                        .email-footer {{
                            background-color: #f9fafb;
                            padding: 15px;
                            text-align: center;
                            font-size: 12px;
                            color: #6b7280;
                        }}
                        .order-details {{
                            margin-top: 20px;
                            margin-bottom: 20px;
                            border: 1px solid #e5e7eb;
                            border-radius: 8px;
                            overflow: hidden;
                        }}
                        .order-header {{
                            background-color: #f3f4f6;
                            padding: 12px;
                            font-weight: bold;
                        }}
                        table {{
                            width: 100%;
                            border-collapse: collapse;
                        }}
                        th {{
                            padding: 12px;
                            text-align: left;
                            background-color: #f9fafb;
                            border-bottom: 1px solid #e5e7eb;
                        }}
                        .summary-row {{
                            background-color: #f9fafb;
                            font-weight: bold;
                        }}
                        .button {{
                            display: inline-block;
                            background-color: #4f46e5;
                            color: white;
                            text-decoration: none;
                            padding: 12px 24px;
                            border-radius: 4px;
                            font-weight: bold;
                            margin: 20px 0;
                        }}
                        .button:hover {{
                            background-color: #4338ca;
                        }}
                    </style>
                </head>
                <body>
                    <div class='email-container'>
                        <div class='email-header'>
                            <h1>WebSiteBanHang</h1>
                        </div>
                        <div class='email-body'>
                            <h2>Xác nhận đơn hàng</h2>
                            <p>Xin chào {model.CustomerName},</p>
                            <p>Cảm ơn bạn đã đặt hàng tại WebSiteBanHang. Dưới đây là thông tin chi tiết về đơn hàng của bạn:</p>
                            
                            <div class='order-details'>
                                <div class='order-header'>
                                    Mã đơn hàng: #{orderId} - {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}
                                </div>
                                
                                <table>
                                    <thead>
                                        <tr>
                                            <th>Sản phẩm</th>
                                            <th style='text-align: center;'>Số lượng</th>
                                            <th style='text-align: right;'>Đơn giá</th>
                                            <th style='text-align: right;'>Thành tiền</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {productList}
                                    </tbody>
                                    <tfoot>
                                        <tr class='summary-row'>
                                            <td colspan='3' style='padding: 12px; text-align: right; border-top: 1px solid #e5e7eb;'>
                                                Tạm tính:
                                            </td>
                                            <td style='padding: 12px; text-align: right; border-top: 1px solid #e5e7eb;'>
                                                {model.Subtotal.ToString("N0")} đ
                                            </td>
                                        </tr>
                                        <tr class='summary-row'>
                                            <td colspan='3' style='padding: 12px; text-align: right;'>
                                                Thuế (10%):
                                            </td>
                                            <td style='padding: 12px; text-align: right;'>
                                                {model.Tax.ToString("N0")} đ
                                            </td>
                                        </tr>
                                        <tr class='summary-row'>
                                            <td colspan='3' style='padding: 12px; text-align: right;'>
                                                Phí vận chuyển:
                                            </td>
                                            <td style='padding: 12px; text-align: right;'>
                                                {model.ShippingFee.ToString("N0")} đ
                                            </td>
                                        </tr>
                                        <tr class='summary-row'>
                                            <td colspan='3' style='padding: 12px; text-align: right; border-top: 2px solid #e5e7eb;'>
                                                Tổng cộng:
                                            </td>
                                            <td style='padding: 12px; text-align: right; border-top: 2px solid #e5e7eb; font-size: 1.1em;'>
                                                {model.Total.ToString("N0")} đ
                                            </td>
                                        </tr>
                                    </tfoot>
                                </table>
                            </div>
                            
                            <h3>Thông tin giao hàng</h3>
                            <p>
                                <strong>Địa chỉ giao hàng:</strong><br>
                                {model.CustomerName}<br>
                                {model.ShippingAddress}<br>
                                {model.City}, {model.State} {model.ZipCode}
                            </p>
                            
                            <p>Đơn hàng của bạn đang được xử lý và sẽ được giao đến trong thời gian sớm nhất. Chúng tôi sẽ thông báo cho bạn khi đơn hàng được vận chuyển.</p>
                            
                            <p>Nếu bạn có bất kỳ câu hỏi nào về đơn hàng, vui lòng liên hệ với chúng tôi qua email hoặc số điện thoại hỗ trợ.</p>
                            
                            <p>Trân trọng,<br>Đội ngũ WebSiteBanHang</p>
                        </div>
                        <div class='email-footer'>
                            <p>© {DateTime.Now.Year} WebSiteBanHang. Tất cả các quyền được bảo lưu.</p>
                        </div>
                    </div>
                </body>
                </html>";

                if (!string.IsNullOrEmpty(email))
                {
                    await _emailSender.SendEmailAsync(
                        email,
                        "Xác nhận đơn hàng - WebSiteBanHang",
                        emailTemplate);
                }
                
                // Xử lý đặt hàng ở đây (lưu vào cơ sở dữ liệu)
                // ...
                
                // Xóa giỏ hàng sau khi đặt hàng thành công
                await _cartService.ClearCartAsync();
                
                // Lưu thông tin đơn hàng vào TempData để hiển thị trong trang xác nhận
                TempData["OrderId"] = orderId;
                TempData["OrderDate"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                TempData["CustomerName"] = model.CustomerName;
                TempData["CustomerEmail"] = email;
                
                // Chuyển hướng đến trang xác nhận đơn hàng
                return RedirectToAction("OrderConfirmation");
            }
            
            // Nếu ModelState không hợp lệ, lấy lại dữ liệu để hiển thị lại trang
            var cartItemsForModel = await _cartService.GetCartItemsAsync();
            var categories = await _categoryRepository.GetAllCategoriesAsync();
            
            ViewBag.Categories = categories;
            model.CartItems = cartItemsForModel;
            
            return View(model);
        }
        
        public IActionResult OrderConfirmation()
        {
            ViewBag.OrderId = TempData["OrderId"];
            ViewBag.OrderDate = TempData["OrderDate"];
            ViewBag.CustomerName = TempData["CustomerName"];
            ViewBag.CustomerEmail = TempData["CustomerEmail"];
            
            return View();
        }
    }
} 