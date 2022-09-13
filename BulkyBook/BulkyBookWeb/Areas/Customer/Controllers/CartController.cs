using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;


namespace BulkyBookWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller //---------------- Cart Controller -----------------
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }
        public CartController(IUnitOfWork unitOfWork, IEmailSender emailSender)
        {
            _unitOfWork = unitOfWork;   
            _emailSender = emailSender;
        }

        //=====================================  When you click cart icon then after open the Index page =================================
        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            ShoppingCartVM = new ShoppingCartVM()
            {
                ListCart = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value,
                includeProperties: "Product"),
                OrderHeader = new()
            };
            foreach (var cart in ShoppingCartVM.ListCart)
            {
                cart.Price = GetPriceBasedOnQuantity(cart.Count, cart.Product.Price,
                    cart.Product.Price50, cart.Product.Price100);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }

        //--------------------------------- Plus icon In Index page ----------------------------------
        public IActionResult Plus(int cartId)
        {
            var cart = _unitOfWork.ShoppingCart.GetFirstOrDefault(u => u.Id == cartId);
            _unitOfWork.ShoppingCart.IncrementCount(cart, 1);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        //--------------------------------- Minus icon In Index page ----------------------------------
        public IActionResult Minus(int cartId)
        {
            var cart = _unitOfWork.ShoppingCart.GetFirstOrDefault(u => u.Id == cartId);
            if (cart.Count <= 1)
            {
                _unitOfWork.ShoppingCart.Remove(cart);
                var count = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cart.ApplicationUserId).ToList().Count-1;
                HttpContext.Session.SetInt32(SD.SessionCart, count);
            }
            else
            {
                _unitOfWork.ShoppingCart.DecrementCount(cart, 1);
            }
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        //--------------------------------- Remove icon In Index page ----------------------------------
        public IActionResult Remove(int cartId)
        {
            var cart = _unitOfWork.ShoppingCart.GetFirstOrDefault(u => u.Id == cartId);
            _unitOfWork.ShoppingCart.Remove(cart);
            _unitOfWork.Save();
            var count = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cart.ApplicationUserId).ToList().Count;
            HttpContext.Session.SetInt32(SD.SessionCart, count);
            return RedirectToAction(nameof(Index));
        }

        //-------------------------------- Show Order Summery ------------------------------------------        
        public IActionResult Summary()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
           

            ShoppingCartVM = new ShoppingCartVM()
            {
                ListCart = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value, includeProperties: "Product"),
                OrderHeader = new OrderHeader()               
            };
            ShoppingCartVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.GetFirstOrDefault(x => x.Id == claim.Value);

            ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
            ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
            ShoppingCartVM.OrderHeader.StreetAddress = ShoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
            ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
            ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
            ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;

            foreach (var cart in ShoppingCartVM.ListCart)
            {
                cart.Price = GetPriceBasedOnQuantity(cart.Count, cart.Product.Price,
                    cart.Product.Price50, cart.Product.Price100);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }

        [HttpPost]
        public IActionResult Summary(ShoppingCartVM shoppingCart)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            shoppingCart.ListCart = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value, includeProperties: "Product");

            shoppingCart.OrderHeader.OrderStatus = OrderStatus.StatusPending;
            shoppingCart.OrderHeader.PaymentStatus = PaymentStatus.StatusPending;
            shoppingCart.OrderHeader.OrderDate = DateTime.Now;
            shoppingCart.OrderHeader.ApplicationUserId = claim.Value;

          
            foreach (var cart in shoppingCart.ListCart)
            {
                shoppingCart.OrderHeader.OrderTotal += (cart.Product.Price * cart.Count);
            }
            _unitOfWork.OrderHeader.Add(shoppingCart.OrderHeader);
            _unitOfWork.Save();

            foreach (var item in shoppingCart.ListCart)
            {
                OrderDetail order = new OrderDetail()
                {
                    ProductId = item.ProductId,
                    OrderId = shoppingCart.OrderHeader.Id,
                    Count=item.Count,
                    Price=item.Product.Price
                };

                _unitOfWork.OrderDetail.Add(order);
                _unitOfWork.Save();
            }
            var Domain = "https://localhost:7082/";

            var options = new SessionCreateOptions
            {
                LineItems = new List<SessionLineItemOptions>(),        

                Mode = "payment",
                SuccessUrl = Domain + $"Customer/Cart/OrderConfirmation?id={shoppingCart.OrderHeader.Id}",
                CancelUrl = Domain + $"Customer/Cart/Index"               
            };
            
            foreach(var item in shoppingCart.ListCart)
            {
                var lineItemsOptions = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Product.Price * 100),
                        Currency = "INR",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Title,
                        },
                    },
                    Quantity = item.Count,
                };
                options.LineItems.Add(lineItemsOptions);
            }

            var service = new SessionService();
            Session session = service.Create(options);

            _unitOfWork.OrderHeader.PaymentStatus(shoppingCart.OrderHeader.Id, session.Id, session.PaymentIntentId);
            _unitOfWork.Save();

            _unitOfWork.ShoppingCart.RemoveRange(shoppingCart.ListCart);
            _unitOfWork.Save();

            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);

            return RedirectToAction("Index", "Home");
        }


        public IActionResult OrderConfirmation(int id)
        {
            var orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(x => x.Id == id);
            var service = new SessionService();
            Session session = service.Get(orderHeader.SessionId);
            if(session.PaymentStatus.ToLower()=="paid")
            {
                _unitOfWork.OrderHeader.UpdateStatus(id, OrderStatus.StatusApproved, PaymentStatus.StatusApproved);

            }
            List<ShoppingCart> cart = _unitOfWork.ShoppingCart.GetAll(x => x.ApplicationUserId == orderHeader.ApplicationUserId).ToList();
            _unitOfWork.ShoppingCart.RemoveRange(cart);
            _unitOfWork.Save();
            return View(id);
        }

        //--------------------------------- Decide a Price In Index page -------------------------------
        private double GetPriceBasedOnQuantity(double quantity, double price, double price50, double price100)
        {
            if (quantity <= 50)
            {
                return price;
            }
            else
            {
                if (quantity <= 100)
                {
                    return price50;
                }
                return price100;
            }
        }
    }
}
