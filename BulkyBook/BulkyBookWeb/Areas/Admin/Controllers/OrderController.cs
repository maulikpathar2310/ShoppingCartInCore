using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class OrderController : Controller
    {
        private IUnitOfWork _unitOfWork;

        public OrderController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        #region APICALL

        public IActionResult AllOrders(string status)
        {
            IEnumerable<OrderHeader> orderHeader;



            if (User.IsInRole("Admin") || User.IsInRole("Employee"))
            {
                orderHeader = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser");
            }
            else
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var claims = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
                orderHeader = _unitOfWork.OrderHeader.GetAll(x => x.ApplicationUserId == claims.Value);
            }

            switch (status)
            {
                case "pending":
                    orderHeader = orderHeader.Where(x => x.PaymentStatus == PaymentStatus.StatusPending);
                    break;
                case "approved":
                    orderHeader = orderHeader.Where(x => x.PaymentStatus == PaymentStatus.StatusApproved);
                    break;
                case "underprocess":
                    orderHeader = orderHeader.Where(x => x.OrderStatus == OrderStatus.StatusInProcess);
                    break;
                case "shipped":
                    orderHeader = orderHeader.Where(x => x.OrderStatus == OrderStatus.StatusShipped);
                    break;
                default:
                    break;
            }

            return Json(new { data = orderHeader });

        }
        #endregion

        public IActionResult Index()
        {
            return View();
        }
    }
}
