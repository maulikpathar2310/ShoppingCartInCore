﻿var dataTable;
$(document).ready(function () {
    var url = window.location.search;
    if (url.includes("pending")) {
        OrderTable("pending");
    }
    else {
        if (url.includes("approved")) {
            OrderTable("approved");
        }

        else {
            if (url.includes("shipped")) {
                OrderTable("shipped");
            }
            else {
                if (url.includes("underprocess")) {
                    OrderTable("underprocess");
                }
                else {
                    OrderTable("all");
                }
            }
        }
    }
});
function OrderTable(status) {
    dataTable = $("#myTable").DataTable({



        "ajax": { "url": "/Admin/Order/AllOrders?status=" + status },
        "columns": [
            { "data": "name" },
            { "data": "phoneNumber" },
            { "data": "orderStatus" },
            { "data": "orderTotal" },
            {
                "data": "id",
                "render": function (data) {
                    return `
                            <a href="/Admin/Order/OrderDetails?id=${data}"><i class="bi bi-pencil-square"></i></a>
                            `
                }
            }
        ]
    });
}