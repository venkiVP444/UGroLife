using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using UGroLifeAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using UGroLifeAPI.Models;
using System;
using System.Linq;
using System.Collections.Generic;

// --- Add these for Email Sending ---
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
// --- End Email Sending additions ---

// Removed: using Microsoft.AspNetCore.Hosting; // No longer needed for logo embedding
// Removed: using System.IO; // No longer needed for Path, FileStream for logo


namespace UGroLifeAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        // Removed: private readonly IWebHostEnvironment _webHostEnvironment; // No longer needed

        // --- SMTP Credentials for Email Sending (for local development) ---
        private readonly string _sendingGmailAddress = "lineandleaf25@gmail.com"; // Your sending Gmail address
        private readonly string _gmailAppPassword = "yrcy krxb izun whhp";     // Your Gmail App Password
        // ------------------------------------------------------------------

        // Modified constructor: Removed IWebHostEnvironment
        public OrderController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            // Removed: _webHostEnvironment = webHostEnvironment;
        }

        // --- DTOs (Data Transfer Objects) ---
        public class CartItemDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
            public int Quantity { get; set; }
        }

        public class OrderItemDto
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }

        public class OrderDto
        {
            public int OrderId { get; set; }
            public string UserId { get; set; }
            public string Email { get; set; }
            public string OrderDate { get; set; }
            public decimal TotalAmount { get; set; }
            public string Status { get; set; }
            public string PaymentStatus { get; set; }
            public string? GatewayOrderId { get; set; }
            public string? GatewayPaymentId { get; set; }
            public string? PaymentSignature { get; set; }
            public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
        }

        public class CreatePaymentOrderRequestDto
        {
            public List<CartItemDto> CartItems { get; set; }
        }

        public class CreatePaymentOrderResponseDto
        {
            public int LocalOrderId { get; set; }
            public string? RazorpayOrderId { get; set; }
            public decimal Amount { get; set; }
            public string Currency { get; set; }
            public string? KeyId { get; set; }
            public string? UserName { get; set; }
            public string? UserEmail { get; set; }
            public string? UserPhoneNumber { get; set; }
            public string? PaymentStatus { get; set; }
        }

        public class VerifyPaymentRequestDto
        {
            public int LocalOrderId { get; set; }
            public string RazorpayPaymentId { get; set; }
            public string RazorpayOrderId { get; set; }
            public string RazorpaySignature { get; set; }
        }


        // --- API Endpoints ---

        [HttpPost("CreatePaymentOrder")]
        [Authorize]
        public async Task<IActionResult> CreatePaymentOrder([FromBody] CreatePaymentOrderRequestDto request)
        {
            if (request.CartItems == null || !request.CartItems.Any())
            {
                return BadRequest("Cart is empty.");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized("User ID not found in token.");
            }
            string userId = userIdClaim.Value;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userId);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            string userFullName = user.FullName;
            string userEmail = user.Email; 

            decimal totalAmount = request.CartItems.Sum(item => item.Price * item.Quantity);

            var orderStatus = totalAmount <= 0 ? "Completed" : "Processing";
            var paymentStatus = totalAmount <= 0 ? "Free" : "Paid";

            var order = new Models.Order
            {
                UserId = userFullName,
                Email = userEmail,
                OrderDate = DateTime.UtcNow,
                TotalAmount = totalAmount,
                Status = orderStatus,
                PaymentStatus = paymentStatus,
                GatewayOrderId = null,
                GatewayPaymentId = null,
                PaymentSignature = null
            };
            _context.Orders.Add(order);
            await _context.SaveChangesAsync(); 

            // Add order items
            var orderItems = new List<OrderItem>();
            foreach (var itemDto in request.CartItems)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = itemDto.Id,
                    ProductName = itemDto.Name,
                    Quantity = itemDto.Quantity,
                    UnitPrice = itemDto.Price
                };
                orderItems.Add(orderItem);
                _context.OrderItems.Add(orderItem);
            }
            await _context.SaveChangesAsync(); // Save order items

            // --- Send Order Confirmation Email to User ---
            try
            {
                await SendOrderConfirmationEmail(user.Email, user.FullName, order, orderItems);
            }
            catch (Exception ex)
            {
                // Log the email sending error, but do not prevent order creation success
                Console.WriteLine($"Error sending order confirmation email to {user.Email} for Order ID {order.OrderId}: {ex.Message}");
            }
            // --- End Email Sending ---

            return Ok(new CreatePaymentOrderResponseDto
            {
                LocalOrderId = order.OrderId,
                RazorpayOrderId = null,
                Amount = totalAmount * 100,
                Currency = "INR",
                KeyId = null,
                UserName = user.FullName,
                UserEmail = user.Email,
                UserPhoneNumber = string.Empty,
                PaymentStatus = paymentStatus
            });
        }

        // --- Modified private method to send order confirmation email (logo embedding removed) ---
        private async Task SendOrderConfirmationEmail(string recipientEmail, string recipientName, Models.Order order, List<OrderItem> items)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("UGroLife", _sendingGmailAddress));
            message.To.Add(new MailboxAddress(recipientName, recipientEmail));
            message.Subject = $"UGroLife: Your Order #{order.OrderId} Confirmation";

            var bodyBuilder = new BodyBuilder();

            // --- REMOVED: Logo embedding logic ---
            // var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "UGroLogo.png");
            // string logoContentId = "ugrolifelogo";
            // if (System.IO.File.Exists(logoPath))
            // {
            //     var image = bodyBuilder.LinkedResources.Add(logoPath);
            //     image.ContentId = logoContentId;
            //     image.IsAttachment = false;
            // }
            // else
            // {
            //     Console.WriteLine($"Warning: Logo file not found at {logoPath}. Email will be sent without embedded logo.");
            // }
            // --- END REMOVED: Logo embedding logic ---

            // Modified call to GenerateOrderConfirmationHtml: removed logoContentId parameter
            bodyBuilder.HtmlBody = GenerateOrderConfirmationHtml(recipientName, order, items);
            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_sendingGmailAddress, _gmailAppPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }

        // Helper method to generate the HTML content for the order confirmation email
        // Modified: Removed logoContentId parameter and logo HTML
        private string GenerateOrderConfirmationHtml(string customerName, Models.Order order, List<OrderItem> items)
        {
            string itemDetailsHtml = "";
            foreach (var item in items)
            {
                itemDetailsHtml += $@"
                    <tr>
                        <td style=""border: 1px solid #ddd; padding: 8px;"">{item.ProductName}</td>
                        <td style=""border: 1px solid #ddd; padding: 8px; text-align: center;"">{item.Quantity}</td>
                        <td style=""border: 1px solid #ddd; padding: 8px; text-align: right;"">₹{item.UnitPrice:F2}</td>
                        <td style=""border: 1px solid #ddd; padding: 8px; text-align: right;"">₹{(item.Quantity * item.UnitPrice):F2}</td>
                    </tr>";
            }

            // --- REMOVED: Logo HTML generation ---
            // string logoHtml = System.IO.File.Exists(Path.Combine(_webHostEnvironment.WebRootPath, "images", "UGroLogo.png")) ?
            //                           $"<img src=\"cid:{logoContentId}\" alt=\"UGroLife Logo\" style=\"width:100px; height:100px; border-radius: 50%;\">" :
            //                           "<p>UGroLife Logo</p>";
            // --- END REMOVED: Logo HTML generation ---

            return $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
                <meta charset=""UTF-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>UGroLife Order Confirmation</title>
                <style>
                    body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 20px auto; padding: 20px; border: 1px solid #eee; border-radius: 8px; box-shadow: 0 0 10px rgba(0,0,0,0.05); }}
                    .header {{ background-color: #4CAF50; color: #ffffff; padding: 10px 20px; text-align: center; border-radius: 8px 8px 0 0; }}
                    .header h1 {{ margin: 0; font-size: 24px; }}
                    .content {{ padding: 20px; }}
                    .footer {{ text-align: center; margin-top: 30px; font-size: 12px; color: #777; }}
                    .button {{ display: inline-block; padding: 10px 20px; margin-top: 20px; background-color: #0cae00; color: #ffffff; text-decoration: none; border-radius: 5px; }}
                    table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
                    th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                    th {{ background-color: #f2f2f2; }}
                    .total-row {{ font-weight: bold; background-color: #f9f9f9; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>Thank You for Your UGroLife Order!</h1>
                    </div>
                    <div class=""content"">
                        <p>Dear {customerName},</p>
                        <p>Thank you for your recent purchase from UGroLife! Your order <strong>#{order.OrderId}</strong> has been successfully placed and is now {order.Status.ToLower()}.</p>
                        <p>We're preparing your fresh microgreens with care. Here are your order details:</p>

                        <table>
                            <thead>
                                <tr>
                                    <th>Product</th>
                                    <th style=""text-align: center;"">Quantity</th>
                                    <th style=""text-align: right;"">Unit Price</th>
                                    <th style=""text-align: right;"">Total</th>
                                </tr>
                            </thead>
                            <tbody>
                                {itemDetailsHtml}
                                <tr class=""total-row"">
                                    <td colspan=""3"" style=""text-align: right;"">Order Total:</td>
                                    <td style=""text-align: right;"">₹{order.TotalAmount:F2}</td>
                                </tr>
                            </tbody>
                        </table>

                        <p>We will notify you again once your order has been shipped or is ready for pickup/delivery.</p>
                        <p>You can view your order status by logging into your account: <a href=""https://UGroLife.com/myorders"" class=""button"">View Your Orders</a></p>
                        <p>If you have any questions, please don't hesitate to contact our support team.</p>
                        <p>Best regards,<br>The UGroLife Team</p>
                        <p></p>
                    </div>
                    <div class=""footer"">
                        <p>&copy; {DateTime.Now.Year} UGroLife. All rights reserved.</p>
                    </div>
                </div>
            </body>
            </html>";
        }


        // --- Commented out VerifyPayment and RazorpayWebhook for local dev ---
        [HttpPost("VerifyPayment")]
        [Authorize]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequestDto request)
        {
            return StatusCode(501, "VerifyPayment endpoint is disabled for local development.");
        }

        [HttpPost("RazorpayWebhook")]
        public async Task<IActionResult> RazorpayWebhook()
        {
            return StatusCode(501, "RazorpayWebhook endpoint is disabled for local development.");
        }
        // --- End commented out methods ---


        [HttpGet("MyOrders")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrders()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
            {
                return Unauthorized("User ID claim not found in token. Authentication failed.");
            }

            try
            {
                var orders = await _context.Orders
                    .Where(o => o.Email == userIdClaim.Value)
                    .OrderByDescending(o => o.OrderDate)
                    .Include(o => o.OrderItems)
                    .Select(o => new OrderDto
                    {
                        OrderId = o.OrderId,
                        UserId = o.UserId,
                        Email = o.Email,
                        OrderDate = o.OrderDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        TotalAmount = o.TotalAmount,
                        Status = o.Status,
                        PaymentStatus = o.PaymentStatus,
                        GatewayOrderId = o.GatewayOrderId,
                        GatewayPaymentId = o.GatewayPaymentId,
                        PaymentSignature = o.PaymentSignature,
                        Items = o.OrderItems.Select(oi => new OrderItemDto
                        {
                            ProductId = oi.ProductId,
                            ProductName = oi.ProductName,
                            Quantity = oi.Quantity,
                            UnitPrice = oi.UnitPrice
                        }).ToList()
                    })
                    .ToListAsync();

                if (!orders.Any())
                {
                    return Ok(new List<OrderDto>());
                }

                return Ok(orders);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OrderController] Error fetching orders for user {userIdClaim.Value}: {ex.Message}");
                return StatusCode(500, "An internal server error occurred while retrieving your orders.");
            }
        }
    }
}