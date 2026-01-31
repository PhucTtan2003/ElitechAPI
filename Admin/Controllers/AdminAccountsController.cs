using Elitech.Models;
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elitech.Admin.Controllers
{
    // File: Admin/Controllers/AdminAccountsController.cs
    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin/accounts")]
    public class AdminAccountsController : ControllerBase
    {
        private readonly AccountService _accountService;

        public AdminAccountsController(AccountService accountService)
        {
            _accountService = accountService;
        }

        // GET: /api/admin/accounts
        // Optional: /api/admin/accounts?role=admin|user
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? role = null)
        {
            try
            {
                List<AccountViewModel> items;

                if (!string.IsNullOrWhiteSpace(role))
                {
                    // hỗ trợ truyền "admin"/"user"
                    items = await _accountService.GetByRoleAsync(role);
                }
                else
                {
                    items = await _accountService.GetAllAsync();
                }

                // Trả về đúng fields JS đang dùng:
                // username, role, isActive, createdAtUtc
                var data = items.Select(x => new
                {
                    username = x.Username,
                    role = x.Role,             // RoleViewModel
                    isActive = x.IsActive,
                    phone = x.Phone,
                    createdAtUtc = x.CreatedAtUtc
                }).ToList();

                return Ok(new
                {
                    code = 0,
                    message = "OK",
                    data
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = 500,
                    message = "Server error",
                    detail = ex.Message
                });
            }
        }

        // (OPTIONAL) PATCH: /api/admin/accounts/{username}/active
        // body: { "isActive": true/false }
        public class SetActiveReq
        {
            public bool IsActive { get; set; }
        }

        [HttpPatch("{username}/active")]
        public async Task<IActionResult> SetActive([FromRoute] string username, [FromBody] SetActiveReq req)
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest(new { code = 400, message = "username is required" });

            try
            {
                var ok = await _accountService.SetActiveAsync(username, req.IsActive);
                if (!ok)
                    return NotFound(new { code = 404, message = "Account not found or not updated" });

                return Ok(new { code = 0, message = "OK", data = new { username, isActive = req.IsActive } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { code = 500, message = "Server error", detail = ex.Message });
            }
        }
    }
}
