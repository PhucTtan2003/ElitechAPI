using Elitech.Data;
using Elitech.Models;
using Elitech.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// =========================
// Mongo
// =========================
builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<ElitechAlertRuleService>();
builder.Services.AddSingleton<ElitechAlertEngine>();
builder.Services.AddSingleton<ElitechAlertEventService>();
builder.Services.AddSingleton<ElitechRealtimeCacheService>();

builder.Services.AddHostedService<Elitech.Workers.ElitechAlertWorker>();


// =========================
// Services
// =========================
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<ElitechDeviceAssignmentService>();

// Elitech API client + options
builder.Services.AddHttpClient<ElitechApiClient>();

builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>().GetSection("Elitech");
    return new ElitechOptions
    {
        BaseUrl = cfg["BaseUrl"] ?? "http://new.i-elitech.com",
        KeyId = cfg["KeyId"] ?? "",
        KeySecret = cfg["KeySecret"] ?? "",
        UserName = cfg["UserName"] ?? "",
        Password = cfg["Password"] ?? ""
    };
});

// MVC
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
    // 👇 dùng chung cho cả API & MVC
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Controllers (attribute routes)
app.MapControllers();

// Conventional route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();
