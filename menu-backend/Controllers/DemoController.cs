using menu_backend.Data;
using menu_backend.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

/// <summary>
/// Provides demo/seed data information for testing purposes.
/// Remove this controller in production.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    [HttpGet("info")]
    public IActionResult GetDemoInfo()
    {
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Demo credentials and endpoints for testing",
            Data = new
            {
                TenantId = SeedData.DemoTenantId,
                Users = new[]
                {
                    new { Email = "admin@demo.com", Password = "admin123", Role = "RestaurantAdmin" },
                    new { Email = "kitchen@demo.com", Password = "kitchen123", Role = "Kitchen" },
                    new { Email = "waiter@demo.com", Password = "waiter123", Role = "Waiter" }
                },
                SampleEndpoints = new
                {
                    Login = "POST /api/auth/login  → { email, password, tenantId }",
                    PublicMenu = $"GET /api/menu/public/{SeedData.DemoTenantId}",
                    LiveOrders = "GET /api/orders/live  (requires auth token)",
                    Dashboard = "GET /api/analytics/dashboard  (requires auth token)",
                    AllTables = "GET /api/tables  (requires auth token)"
                },
                Notes = new[]
                {
                    "Use 'X-Tenant-Id: demo-restaurant-001' header OR include tenantId in login request",
                    "Add 'Authorization: Bearer <token>' header after login",
                    "Customer flow: Public menu → Add to cart → Create order → Track order",
                    "Admin flow: Login → Dashboard / Menu management / Orders / Tables",
                    "Kitchen flow: Login → Live orders (KDS)"
                }
            }
        });
    }
}
