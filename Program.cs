using Elitech.Data;
using Elitech.Models;
using Elitech.Services;
using Elitech.Middleware;
using Elitech.Hubs;
using Elitech.Infrastructure;

using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// =========================
// Mongo
// =========================
builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));
builder.Services.AddSingleton<MongoContext>();

// =========================
// Alert (Rules / Engine / Events) + Worker
// =========================
builder.Services.AddSingleton<ElitechAlertRuleService>();
builder.Services.AddSingleton<ElitechAlertEngine>();
builder.Services.AddSingleton<ElitechAlertEventService>();

builder.Services.AddHostedService<Elitech.Workers.ElitechAlertWorker>();

// =========================
// Services
// =========================
builder.Services.Configure<SessionPolicyOptions>(builder.Configuration.GetSection("SessionPolicy"));

builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<LoginSessionService>();
builder.Services.AddScoped<ElitechDeviceAssignmentService>();

// Elitech API client + options
builder.Services.AddHttpClient<ElitechApiClient>();

// options
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>().GetSection("Elitech");
    return new ElitechOptions
    {
        BaseUrl = cfg["BaseUrl"] ?? "https://new.i-elitech.com",
        KeyId = cfg["KeyId"] ?? "",
        KeySecret = cfg["KeySecret"] ?? "",
        UserName = cfg["UserName"] ?? "",
        Password = cfg["Password"] ?? ""
    };
});

builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>().GetSection("ElitechAlarm");
    return new ElitechAlarmOptions
    {
        BaseUrl = cfg["BaseUrl"] ?? "https://i-elitech.com",
        KeyId = cfg["KeyId"] ?? "",
        KeySecret = cfg["KeySecret"] ?? "",
        UserName = cfg["UserName"] ?? "",
        Password = cfg["Password"] ?? ""
    };
});
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();

// Worker push alarm realtime
builder.Services.AddHostedService<ElitechAlarmRealtimeWorker>();

// http clients
builder.Services.AddHttpClient("ElitechMain", c => { });
builder.Services.AddHttpClient("ElitechAlarm", c => { });

// api client (manual DI)
builder.Services.AddSingleton<ElitechApiClient>();

// =========================
// MVC
// =========================
builder.Services.AddControllersWithViews();

// =========================
// Auth (Cookie)
// =========================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.Cookie.Name = "Elitech.Auth";
        opt.LoginPath = "/Login";
        opt.AccessDeniedPath = "/Login/Denied";
        opt.SlidingExpiration = true;

        // NOTE:
        // Cookie chỉ là vé vào cổng (session thật nằm trong Mongo).
        // Touch session sẽ do /api/session/keepalive (hoặc endpoint tương tự) gọi khi có tương tác thật.
        // Auto-logout do SessionGuardMiddleware xử lý.
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// =========================
// Seed
// =========================
using (var scope = app.Services.CreateScope())
{
    var accSvc = scope.ServiceProvider.GetRequiredService<AccountService>();
    await accSvc.SeedAsync();
}

// =========================
// Middleware
// =========================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

// ✅ SessionGuardMiddleware: SAU auth, TRƯỚC authorization
app.UseMiddleware<SessionGuardMiddleware>();

app.UseAuthorization();

// Controllers (attribute routes)
app.MapControllers();
app.MapHub<ElitechAlarmHub>("/hubs/alarm");

// Conventional route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();
