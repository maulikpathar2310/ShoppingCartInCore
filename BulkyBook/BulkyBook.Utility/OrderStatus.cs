using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.Utility
{
    public static class OrderStatus
    {
        public const string StatusPending = "Pending";
        public const string StatusRefund = "Refund ";
        public const string StatusApproved = "Approved ";
        public const string StatusCancel = "Cancel";
        public const string StatusInProcess = "UnderProcessing";
        public const string StatusShipped = "Shipped";
    }
}
