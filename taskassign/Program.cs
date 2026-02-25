using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using taskassign.Data;
using taskassign.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<SmtpEmailOptions>(builder.Configuration.GetSection(SmtpEmailOptions.SectionName));

var smtpConfig = builder.Configuration.GetSection(SmtpEmailOptions.SectionName);
var hasSmtpConfig = !string.IsNullOrWhiteSpace(smtpConfig["Host"])
    && !string.IsNullOrWhiteSpace(smtpConfig["Username"])
    && !string.IsNullOrWhiteSpace(smtpConfig["Password"])
    && !string.IsNullOrWhiteSpace(smtpConfig["FromEmail"]);

if (hasSmtpConfig)
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, LogEmailSender>();
}
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });

var app = builder.Build();

var smtpConfigCheck = app.Configuration.GetSection(SmtpEmailOptions.SectionName);
var hasSmtpConfigCheck = !string.IsNullOrWhiteSpace(smtpConfigCheck["Host"])
    && !string.IsNullOrWhiteSpace(smtpConfigCheck["Username"])
    && !string.IsNullOrWhiteSpace(smtpConfigCheck["Password"])
    && !string.IsNullOrWhiteSpace(smtpConfigCheck["FromEmail"]);

if (!hasSmtpConfigCheck)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("SMTP configuration is incomplete. Email sending is disabled. Emails will be logged only. " +
                      "To enable email sending, configure User Secrets (development) or environment variables (production). " +
                      "See SECURITY_SETUP.md for details.");
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Task}/{action=Dashboard}/{id?}");

app.Run();
