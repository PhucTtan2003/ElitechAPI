using Elitech.Services;
using Elitech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Elitech.Controllers
{
    public class LoginController : Controller
    {
        private readonly AccountService _accounts;
        public LoginController(AccountService accounts) => _accounts = accounts;

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

        [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
        public async Task<IActionResult> Index(LoginViewModel model, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ thông tin.");
                return View(model);
            }

            if (!await _accounts.ValidatePasswordAsync(model.Username, model.Password))
            {
                ModelState.AddModelError("", "Sai tài khoản hoặc mật khẩu.");
                return View(model);
            }

            var acc = await _accounts.GetByUsernameAsync(model.Username);
            if (acc is null || !acc.IsActive)
            {
                ModelState.AddModelError("", "Tài khoản không hoạt động.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, acc.Id),
                new Claim(ClaimTypes.Name, acc.Username),
                new Claim(ClaimTypes.Role, acc.Role.ToString())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Router", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            // rồi mới SignInAsync(...)

            return RedirectToAction("Index", "Login");
        }

        [AllowAnonymous]
        public IActionResult Denied() => Content("Bạn không có quyền truy cập.");
    }
}