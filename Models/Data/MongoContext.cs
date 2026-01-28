using Elitech.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Elitech.Data
{
    public class MongoOptions
    {
        public string ConnectionString { get; set; } = "";
        public string Database { get; set; } = "";
    }

    public class MongoContext
    {
        private readonly IMongoDatabase _db;
        public IMongoDatabase Database => _db;
        public MongoContext(IOptions<MongoOptions> opt)
        {
            var client = new MongoClient(opt.Value.ConnectionString);
            _db = client.GetDatabase(opt.Value.Database);
        }

        public IMongoCollection<AccountViewModel> Accounts => _db.GetCollection<AccountViewModel>("Accounts");
        // ✅ thêm cái này để hết lỗi _db.Devices
        public IMongoCollection<DeviceRegistry> Devices
            => _db.GetCollection<DeviceRegistry>("Devices");
    }
}
