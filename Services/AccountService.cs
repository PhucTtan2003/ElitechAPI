using Elitech.Data;
using Elitech.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Elitech.Services
{
    public class AccountService
    {
        private readonly MongoContext _ctx;

        public AccountService(MongoContext ctx)
        {
            _ctx = ctx;
        }

        // =========================
        // BASIC
        // =========================
        public Task<AccountViewModel> GetByUsernameAsync(string username)
            => _ctx.Accounts.Find(x => x.Username == username).FirstOrDefaultAsync();

        public async Task<bool> ValidatePasswordAsync(string username, string password)
        {
            var acc = await GetByUsernameAsync(username);
            if (acc is null || !acc.IsActive) return false;
            return BCrypt.Net.BCrypt.Verify(password, acc.PasswordHash);
        }

        public async Task<AccountViewModel> CreateAsync(string username, string password, RoleViewModel role)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password);

            var acc = new AccountViewModel
            {
                Username = username,
                PasswordHash = hash,
                Role = role,
                IsActive = true,

                // ✅ FIX: để bảng Accounts có ngày tạo (JS đang đọc createdAtUtc)
                CreatedAtUtc = DateTime.UtcNow,

                // profile fields (optional)
                FullName = null,
                Email = null,
                Phone = null,
                //AvatarUrl = null
            };

            await _ctx.Accounts.InsertOneAsync(acc);
            return acc;
        }

        public async Task SeedAsync()
        {
            var idx = new CreateIndexModel<AccountViewModel>(
                Builders<AccountViewModel>.IndexKeys.Ascending(x => x.Username),
                new CreateIndexOptions { Unique = true });

            await _ctx.Accounts.Indexes.CreateOneAsync(idx);

            if (await GetByUsernameAsync("admin") is null)
                await CreateAsync("admin", "Admin@123", RoleViewModel.Admin);

            if (await GetByUsernameAsync("user1") is null)
                await CreateAsync("user1", "User@123", RoleViewModel.User);
        }

        // =========================
        // ✅ DÙNG CHO BẢNG ACCOUNTS
        // =========================
        public async Task<List<AccountViewModel>> GetAllAsync()
        {
            return await _ctx.Accounts
                .Find(Builders<AccountViewModel>.Filter.Empty)
                .SortByDescending(x => x.CreatedAtUtc)
                .ToListAsync();
        }

        // ✅ Chuẩn nhất: filter theo RoleViewModel
        public async Task<List<AccountViewModel>> GetByRoleAsync(RoleViewModel role)
        {
            return await _ctx.Accounts.Find(x => x.Role == role).ToListAsync();
        }

        // ✅ Optional: nếu controller truyền string ("User"/"Admin") thì vẫn dùng được
        public Task<List<AccountViewModel>> GetByRoleAsync(string role)
        {
            var r = (role ?? "").Trim().ToLowerInvariant();
            var parsed = r switch
            {
                "admin" => RoleViewModel.Admin,
                "user" => RoleViewModel.User,
                _ => (RoleViewModel?)null
            };

            if (parsed is null)
                return Task.FromResult(new List<AccountViewModel>());

            return GetByRoleAsync(parsed.Value);
        }

        // =========================
        // (Optional) BẬT/TẮT ACCOUNT
        // =========================
        public async Task<bool> SetActiveAsync(string username, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;

            var update = Builders<AccountViewModel>.Update.Set(x => x.IsActive, isActive);
            var res = await _ctx.Accounts.UpdateOneAsync(x => x.Username == username, update);

            return res.ModifiedCount > 0;
        }

        // =========================================================
        // PROFILE / SETTINGS (MongoContext style)
        // =========================================================

        // ✅ Lấy thông tin profile (theo username)
        public Task<AccountViewModel> GetProfileAsync(string username)
            => _ctx.Accounts.Find(x => x.Username == username).FirstOrDefaultAsync();

        // ✅ Update thông tin cá nhân
        public async Task<bool> UpdateProfileAsync(
            string username,
            string? fullName,
            string? email,
            string? phone,
            string? avatarUrl)
        {
            username = (username ?? "").Trim();
            if (string.IsNullOrWhiteSpace(username)) return false;

            string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

            var update = Builders<AccountViewModel>.Update
                .Set(x => x.FullName, Norm(fullName))
                .Set(x => x.Email, Norm(email))
                .Set(x => x.Phone, Norm(phone));

            // ✅ chỉ set avatar khi có avatar mới
            //if (!string.IsNullOrWhiteSpace(avatarUrl))
            //    update = update.Set(x => x.AvatarUrl, avatarUrl);

            var res = await _ctx.Accounts.UpdateOneAsync(x => x.Username == username, update);
            return res.ModifiedCount > 0;
        }


        // ✅ Đổi mật khẩu (Settings)
        // - kiểm tra mật khẩu hiện tại
        // - hash mật khẩu mới
        public async Task<bool> ChangePasswordAsync(
            string username,
            string currentPassword,
            string newPassword)
        {
            username = (username ?? "").Trim();
            if (string.IsNullOrWhiteSpace(username)) return false;
            if (string.IsNullOrWhiteSpace(currentPassword)) return false;
            if (string.IsNullOrWhiteSpace(newPassword)) return false;

            var acc = await GetByUsernameAsync(username);
            if (acc is null || !acc.IsActive) return false;

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, acc.PasswordHash))
                return false;

            var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            var update = Builders<AccountViewModel>.Update
                .Set(x => x.PasswordHash, newHash);

            var res = await _ctx.Accounts.UpdateOneAsync(
                x => x.Username == username,
                update);

            return res.ModifiedCount > 0;
        }
        //public async Task<AccountViewModel?> GetByUserIdAsync(string userId)
        //{
        //    userId = (userId ?? "").Trim();
        //    if (string.IsNullOrWhiteSpace(userId)) return null;

        //    // TH1: userId là ObjectId (phổ biến nếu claim lưu MongoId)
        //    if (ObjectId.TryParse(userId, out var oid))
        //    {
        //        // AccountViewModel cần có Id kiểu ObjectId
        //        var byId = await _ctx.Accounts.Find(x => x.Id == oid).FirstOrDefaultAsync();
        //        if (byId != null) return byId;
        //    }

        //    // TH2: fallback nếu claim thực tế lại là username
        //    return await GetByUsernameAsync(userId);
        //}

        //public async Task<string?> GetPhoneByUserIdAsync(string userId)
        //{
        //    var acc = await GetByUserIdAsync(userId);
        //    var phone = (acc?.Phone ?? "").Trim();
        //    return string.IsNullOrWhiteSpace(phone) ? null : phone;
        //}

    }
}