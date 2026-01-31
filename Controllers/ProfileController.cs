using Elitech.Models;
using Elitech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Elitech.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly AccountService _accounts;
        private readonly IWebHostEnvironment _env;

        public ProfileController(AccountService accounts, IWebHostEnvironment env)
        {
            _accounts = accounts;
            _env = env;
        }

        // =========================
        // GET /Profile
        // =========================
        [HttpGet("/Profile")]
        public async Task<IActionResult> Index()
        {
            var username = User?.Identity?.Name ?? "";
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("Index", "Login");

            var acc = await _accounts.GetProfileAsync(username);
            if (acc == null)
                return RedirectToAction("Index", "Login");

            acc.PasswordHash = ""; // never show hash
            return View(acc);
        }

        // =========================
        // POST /Profile
        // =========================
        [HttpPost("/Profile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(
            string? fullName,
            string? email,
            string? phone,
            IFormFile? avatar,
            string? currentPassword,
            string? newPassword,
            string? confirmNewPassword)
        {
            var username = User?.Identity?.Name ?? "";
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("Index", "Login");

            // 1) Load account hiện tại (để biết avatar cũ + kiểm tra active)
            var acc = await _accounts.GetProfileAsync(username);
            if (acc == null) return RedirectToAction("Index", "Login");

            // 2) Validate nhẹ email (optional)
            if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
            {
                TempData["Error"] = "Email không hợp lệ.";
                return Redirect("/Profile");
            }

            string? newAvatarUrl = null;

            // =========================
            // Upload avatar (optional) - CÁCH 1: lưu file + xóa file cũ
            // =========================
            if (avatar != null && avatar.Length > 0)
            {
                var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();

                if (!allowedExt.Contains(ext))
                {
                    TempData["Error"] = "Avatar phải là ảnh (.jpg, .png, .webp).";
                    return Redirect("/Profile");
                }

                if (avatar.Length > 2 * 1024 * 1024)
                {
                    TempData["Error"] = "Avatar tối đa 2MB.";
                    return Redirect("/Profile");
                }

                // wwwroot/uploads/avatars
                var folder = Path.Combine(_env.WebRootPath, "uploads", "avatars");
                Directory.CreateDirectory(folder);

                // tên file an toàn + unique
                var safeUser = Regex.Replace(username, @"[^a-zA-Z0-9_\-]", "_");
                var fileName = $"{safeUser}_{Guid.NewGuid():N}{ext}";
                var savePath = Path.Combine(folder, fileName);

                using (var stream = System.IO.File.Create(savePath))
                {
                    await avatar.CopyToAsync(stream);
                }

                newAvatarUrl = $"/uploads/avatars/{fileName}";

                //// Xóa avatar cũ nếu nằm trong /uploads/avatars/
                //if (!string.IsNullOrWhiteSpace(acc.AvatarUrl))
                //{
                //    var oldUrl = acc.AvatarUrl.Trim();
                //    if (oldUrl.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase))
                //   {
                //       var oldPhysicalPath = Path.Combine(
                //            _env.WebRootPath,
                //            oldUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                //        );

                //        if (System.IO.File.Exists(oldPhysicalPath))
                //            System.IO.File.Delete(oldPhysicalPath);
                //    }
                //}
            }

            // =========================
            // Update profile Mongo
            // avatar null => giữ avatar cũ (service bạn đã làm kiểu đó)
            // =========================
            await _accounts.UpdateProfileAsync(username, fullName, email, phone, newAvatarUrl);

            // Reload lại acc để lấy AvatarUrl mới nhất (nếu avatar null thì vẫn lấy cũ)
            acc = await _accounts.GetProfileAsync(username);
            if (acc == null) return RedirectToAction("Index", "Login");

            // =========================
            // Change password (optional)
            // =========================
            if (!string.IsNullOrWhiteSpace(newPassword) || !string.IsNullOrWhiteSpace(confirmNewPassword))
            {
                if (string.IsNullOrWhiteSpace(currentPassword))
                {
                    TempData["Error"] = "Vui lòng nhập mật khẩu hiện tại để đổi mật khẩu.";
                    return Redirect("/Profile");
                }

                if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                {
                    TempData["Error"] = "Mật khẩu mới tối thiểu 6 ký tự.";
                    return Redirect("/Profile");
                }

                if (!string.Equals(newPassword, confirmNewPassword, StringComparison.Ordinal))
                {
                    TempData["Error"] = "Mật khẩu nhập lại không khớp.";
                    return Redirect("/Profile");
                }

                var ok = await _accounts.ChangePasswordAsync(username, currentPassword, newPassword);
                if (!ok)
                {
                    TempData["Error"] = "Mật khẩu hiện tại không đúng hoặc tài khoản không hoạt động.";
                    return Redirect("/Profile");
                }
            }

            // =========================
            // ✅ QUAN TRỌNG: refresh cookie/claims để _Layout đổi avatar ngay
            // =========================
            await RefreshAuthClaimsAsync(acc);

            TempData["Success"] = "Cập nhật thông tin thành công.";
            return Redirect("/Profile");
        }

        [HttpGet("/Settings")]
        public IActionResult Settings() => Redirect("/Profile");

        // =========================
        // helpers
        // =========================
        private static bool IsValidEmail(string email)
        {
            email = email.Trim();
            if (email.Length > 120) return false;
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        private async Task RefreshAuthClaimsAsync(AccountViewModel acc)
        {
            var role = User?.FindFirst(ClaimTypes.Role)?.Value
                       ?? (User?.IsInRole("Admin") == true ? "Admin" : "User");

            var nameId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? (acc.Id ?? "");
            var sid = User?.FindFirst("sid")?.Value ?? "";

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, nameId),
        new Claim(ClaimTypes.Name, acc.Username),
        new Claim(ClaimTypes.Role, role),
        new Claim("sid", sid),
        //new Claim("avatarUrl", acc.AvatarUrl ?? "")
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                }
            );
        }

    }
}