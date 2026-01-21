using Elitech.Services;
using Elitech.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

using System.Security.Claims;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Elitech.Controllers
{
    public class LoginController : Controller
    {
        private readonly AccountService _accounts;
        private readonly LoginSessionService _sessions;

        // ✅ merge: giữ accounts + thêm sessions
        public LoginController(AccountService accounts, LoginSessionService sessions)
        {
            _accounts = accounts;
            _sessions = sessions;
        }

        // =========================
        // GET /Login
        // =========================
        [HttpGet, AllowAnonymous]
        public IActionResult Index(string? returnUrl = null)
        {
            if (User?.Identity?.IsAuthenticated == true)
                return RedirectToAction("Router", "Home", new { returnUrl });

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        public class LoginViewModel
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }

        // =========================
        // POST /Login
        // =========================
        [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
        public async Task<IActionResult> Index(LoginViewModel model, string? returnUrl = null)
        {
            // 1) Validate input
            if (string.IsNullOrWhiteSpace(model.Username) ||
                string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ thông tin.");
                return View(model);
            }

            // 2) Validate password
            if (!await _accounts.ValidatePasswordAsync(model.Username, model.Password))
            {
                ModelState.AddModelError("", "Sai tài khoản hoặc mật khẩu.");
                return View(model);
            }

            // 3) Load account
            var acc = await _accounts.GetByUsernameAsync(model.Username);
            if (acc is null || !acc.IsActive)
            {
                ModelState.AddModelError("", "Tài khoản không hoạt động.");
                return View(model);
            }

            // 4) Create / Resume session (merge từ code 2)
            var session = await _sessions.CreateOrResumeAsync(
                accountId: acc.Id,
                ip: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString()
            );

            var sessionId = session.SessionId;

            // ✅ Role (enum -> string)
            var role = acc.Role.ToString();
            if (role != "Admin" && role != "User")
                role = "User";

            // 5) Claims (giữ NameIdentifier/Name/Role + thêm sid)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, acc.Id),
                new Claim(ClaimTypes.Name, acc.Username),
                new Claim(ClaimTypes.Role, role),
                new Claim("sid", sessionId),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // ✅ merge: SignIn có AuthenticationProperties như code 2
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    AllowRefresh = true,
                    // ⚠️ nếu bạn đã set cookie expire trong Program.cs thì vẫn OK (cái này chỉ là override nhẹ)
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                }
            );

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Router", "Home");
        }

        // =========================
        // POST /Login/Logout
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // ✅ merge: revoke session trước khi sign out
            var sid = User?.Claims?.FirstOrDefault(x => x.Type == "sid")?.Value;

            if (!string.IsNullOrWhiteSpace(sid))
            {
                await _sessions.LogoutBySessionIdAsync(sid);
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Login");
        }

        // =========================
        // API: /api/session/status
        // =========================
        [HttpGet("/api/session/status")]
        [Authorize]
        public async Task<IActionResult> SessionStatus(CancellationToken ct)
        {
            var sid = User?.Claims?.FirstOrDefault(x => x.Type == "sid")?.Value;
            if (string.IsNullOrWhiteSpace(sid))
                return Unauthorized();

            var (_, status) = await _sessions.GetStatusAsync(sid, ct);

            return Ok(new
            {
                status.ServerNowUtc,
                status.ExpiresAtUtc,
                status.RemainingSeconds,
                status.ShouldWarn,
                status.ShouldLogout,
                status.Revoked,
                status.RevokedReason
            });
        }

        // =========================
        // API: /api/session/keepalive
        // =========================
        [HttpPost("/api/session/keepalive")]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> KeepAlive(CancellationToken ct)
        {
            var sid = User?.Claims?.FirstOrDefault(x => x.Type == "sid")?.Value;
            if (string.IsNullOrWhiteSpace(sid))
                return Unauthorized();

            await _sessions.TouchAsync(sid, ct);
            return Ok(new { code = 0 });
        }

        [AllowAnonymous]
        public IActionResult Denied() => Content("Bạn không có quyền truy cập.");
    }
}
