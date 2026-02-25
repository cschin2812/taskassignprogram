using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using taskassign.Data;
using taskassign.Models;
using taskassign.Services;

namespace taskassign.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AccountController> _logger;

    public AccountController(ApplicationDbContext db, IEmailSender emailSender, ILogger<AccountController> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var authenticatedUserId = ResolveCurrentUserId();
            if (CanRedirectToReturnUrl(returnUrl, authenticatedUserId))
            {
                return Redirect(returnUrl!);
            }

            return RedirectToAction("Dashboard", "Task");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        var loginInput = model.Username.Trim();
        var normalizedEmail = loginInput.ToLowerInvariant();

        var user = _db.Users
            .Where(u => (u.Username == loginInput || u.Email == normalizedEmail) && u.Password == model.Password && !u.IsDel)
            .Select(u => new
            {
                u.Id,
                u.Username,
                Role = u.Role ?? "Member",
                DisplayName = u.DisplayName ?? u.Username
            })
            .FirstOrDefault();
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("DisplayName", user.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        if (CanRedirectToReturnUrl(returnUrl, user.Id))
        {
            return Redirect(returnUrl!);
        }

        return RedirectToAction("Dashboard", "Task");
    }

    private bool CanRedirectToReturnUrl(string? returnUrl, int userId)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            return false;
        }

        if (!returnUrl.StartsWith("/Group/AcceptInvite", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (userId <= 0)
        {
            return false;
        }

        var token = ExtractInviteToken(returnUrl);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        return _db.GroupInvites.Any(i => i.Token == token
            && i.InvitedUserId == userId
            && i.Status == GroupInviteStatus.Pending
            && i.ExpiresAt >= now);
    }

    private static string? ExtractInviteToken(string returnUrl)
    {
        var queryStart = returnUrl.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0 || queryStart >= returnUrl.Length - 1)
        {
            return null;
        }

        var query = returnUrl[(queryStart + 1)..];
        var values = QueryHelpers.ParseQuery(query);
        return values.TryGetValue("token", out var tokenValues)
            ? tokenValues.FirstOrDefault()
            : null;
    }

    private int ResolveCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claimValue, out var userId) && userId > 0)
        {
            return userId;
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            return 0;
        }

        return _db.Users
            .Where(u => u.Username == username)
            .Select(u => u.Id)
            .FirstOrDefault();
    }


    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = FindActiveUserByIdentifier(model.Identifier);
        if (user == null)
        {
            ModelState.AddModelError(nameof(model.Identifier), "User not found.");
            return View(model);
        }

        var otpCode = Random.Shared.Next(0, 1_000_000).ToString("D6");
        var generatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        user.OTP = $"RESET:{otpCode}:{generatedAt}";
        _db.SaveChanges();

        _ = Task.Run(async () =>
        {
            try
            {
                await _emailSender.SendAsync(
                    user.Email,
                    "TaskAssign password reset OTP",
                    $"Your password reset OTP is {otpCode}. It will expire in 10 minutes.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset OTP email to {Email}.", user.Email);
            }
        });

        TempData["AccountSuccess"] = "OTP sent to your email. Please reset your password within 10 minutes.";
        return RedirectToAction(nameof(ResetPassword), new { identifier = model.Identifier.Trim() });
    }

    [HttpGet]
    public IActionResult ResetPassword(string? identifier)
    {
        return View(new ResetPasswordViewModel
        {
            Identifier = identifier?.Trim() ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = FindActiveUserByIdentifier(model.Identifier);
        if (user == null)
        {
            ModelState.AddModelError(nameof(model.Identifier), "User not found.");
            return View(model);
        }

        if (!TryParseResetOtp(user.OTP, out var expectedOtp, out var generatedAt))
        {
            ModelState.AddModelError(nameof(model.Otp), "OTP is invalid. Please request a new OTP.");
            return View(model);
        }

        if (DateTimeOffset.UtcNow - generatedAt > TimeSpan.FromMinutes(10))
        {
            user.OTP = null;
            _db.SaveChanges();
            ModelState.AddModelError(nameof(model.Otp), "OTP expired. Please request a new OTP.");
            return View(model);
        }

        if (!string.Equals(expectedOtp, model.Otp.Trim(), StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.Otp), "Incorrect OTP.");
            return View(model);
        }

        user.Password = model.NewPassword.Trim();
        user.OTP = null;
        _db.SaveChanges();

        TempData["AccountSuccess"] = "Password reset successfully. Please login.";
        return RedirectToAction(nameof(Login));
    }

    private AppUser? FindActiveUserByIdentifier(string? identifier)
    {
        var input = identifier?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var normalizedEmail = input.ToLowerInvariant();
        return _db.Users.FirstOrDefault(u => !u.IsDel && (u.Username == input || u.Email == normalizedEmail));
    }

    private static bool TryParseResetOtp(string? payload, out string otp, out DateTimeOffset generatedAt)
    {
        otp = string.Empty;
        generatedAt = DateTimeOffset.MinValue;

        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var parts = payload.Split(':', 3, StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], "RESET", StringComparison.Ordinal) || !long.TryParse(parts[2], out var generatedAtUnix))
        {
            return false;
        }

        otp = parts[1];
        generatedAt = DateTimeOffset.FromUnixTimeSeconds(generatedAtUnix);
        return true;
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Dashboard", "Task");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var normalizedUsername = model.Username.Trim();

        if (_db.Users.Any(u => u.Username == normalizedUsername))
        {
            ModelState.AddModelError(nameof(model.Username), "Username is already taken.");
            return View(model);
        }

        if (_db.Users.Any(u => u.Email == normalizedEmail))
        {
            ModelState.AddModelError(nameof(model.Email), "Email is already registered.");
            return View(model);
        }

        var stalePendingUsers = _db.Users
            .IgnoreQueryFilters()
            .Where(u => (u.Username == normalizedUsername || u.Email == normalizedEmail) && u.IsDel)
            .ToList();
        if (stalePendingUsers.Any())
        {
            _db.Users.RemoveRange(stalePendingUsers);
            _db.SaveChanges();
        }

        var otpCode = Random.Shared.Next(0, 1_000_000).ToString("D6");
        var generatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var otpPayload = $"{otpCode}:{generatedAt}";

        var user = new AppUser
        {
            Username = normalizedUsername,
            Password = model.Password,
            DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? normalizedUsername : model.DisplayName.Trim(),
            Email = normalizedEmail,
            Role = "Member",
            IsDel = true,
            OTP = otpPayload
        };

        _db.Users.Add(user);
        _db.SaveChanges();

        _ = Task.Run(async () =>
        {
            try
            {
                await _emailSender.SendAsync(
                    user.Email,
                    "TaskAssign account verification OTP",
                    $"Your OTP is {otpCode}. It will expire in 10 minutes.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP verification email to {Email}.", user.Email);
            }
        });

        TempData["OtpInfo"] = "An OTP has been sent to your email. Please verify within 10 minutes.";
        return RedirectToAction(nameof(VerifyOtp), new { userId = user.Id });
    }

    [HttpGet]
    public IActionResult VerifyOtp(int userId)
    {
        var user = _db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Id == userId && u.IsDel);
        if (user == null)
        {
            TempData["AccountError"] = "Signup session not found. Please sign up again.";
            return RedirectToAction(nameof(Register));
        }

        return View(new VerifyOtpViewModel { UserId = user.Id, Email = user.Email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyOtp(VerifyOtpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = _db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Id == model.UserId && u.Email == normalizedEmail && u.IsDel);
        if (user == null)
        {
            TempData["AccountError"] = "Signup session expired. Please sign up again.";
            return RedirectToAction(nameof(Register));
        }

        if (string.IsNullOrWhiteSpace(user.OTP) || !user.OTP.Contains(':'))
        {
            TempData["AccountError"] = "OTP session invalid. Please sign up again.";
            return RedirectToAction(nameof(Register));
        }

        var otpParts = user.OTP.Split(':', 2, StringSplitOptions.TrimEntries);
        if (otpParts.Length != 2 || !long.TryParse(otpParts[1], out var generatedAtUnix))
        {
            TempData["AccountError"] = "OTP session invalid. Please sign up again.";
            return RedirectToAction(nameof(Register));
        }

        var generatedAt = DateTimeOffset.FromUnixTimeSeconds(generatedAtUnix);
        var isExpired = DateTimeOffset.UtcNow - generatedAt > TimeSpan.FromMinutes(10);
        if (isExpired)
        {
            _db.Users.Remove(user);
            _db.SaveChanges();
            TempData["AccountError"] = "OTP expired after 10 minutes. Please sign up again.";
            return RedirectToAction(nameof(Register));
        }

        if (!string.Equals(otpParts[0], model.Otp.Trim(), StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.Otp), "Incorrect OTP.");
            return View(model);
        }

        user.IsDel = false;
        user.OTP = null;
        _db.SaveChanges();

        TempData["AccountSuccess"] = "Account created successfully. Please login.";
        return RedirectToAction(nameof(Login));
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
