using Elitech.Data;
using Elitech.Models;
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
                CreatedAtUtc = DateTime.UtcNow
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
    }
}
