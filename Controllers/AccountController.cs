using Microsoft.AspNetCore.Identity;

namespace LibraryManagement.Controllers;

public class AccountController : AppController
{
    private readonly SignInManager<AppUser> _signIn;
    private readonly UserManager<AppUser> _users;
    private readonly AppDbContext _db;

    public AccountController(SignInManager<AppUser> signIn, UserManager<AppUser> users, AppDbContext db)
    {
        _signIn = signIn; _users = users; _db = db;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(string userName, string password, string returnUrl = null)
    {
        var result = await _signIn.PasswordSignInAsync((userName ?? "").Trim(), password ?? "", false, false);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
        ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu.";
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // Sinh viên tự đăng ký tài khoản bằng thông tin trên thẻ thư viện
    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View();

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(string cardNumber, string studentName, string userName, string password)
    {
        cardNumber = (cardNumber ?? "").Trim();
        studentName = (studentName ?? "").Trim();
        userName = (userName ?? "").Trim();

        string error = null;
        var card = await _db.LibraryCards.FirstOrDefaultAsync(c => c.CardNumber == cardNumber);
        if (card == null)
            error = "Không tìm thấy thẻ thư viện với số thẻ này.";
        else if (!string.Equals(card.StudentName, studentName, StringComparison.OrdinalIgnoreCase))
            error = "Họ tên không khớp với thông tin trên thẻ.";
        else if (await _users.Users.AnyAsync(u => u.LibraryCardId == card.Id))
            error = "Thẻ này đã có tài khoản. Vui lòng đăng nhập.";

        if (error == null)
        {
            var user = new AppUser
            {
                UserName = userName,
                FullName = card.StudentName,
                Email = $"{userName}@student.library.local",
                LibraryCardId = card.Id
            };
            var res = await _users.CreateAsync(user, password ?? "");
            if (!res.Succeeded)
                error = string.Join(" ", res.Errors.Select(e => e.Description));
            else
            {
                await _users.AddToRoleAsync(user, "Student");
                await _signIn.SignInAsync(user, false);
                TempData["Success"] = "Đăng ký thành công! Chào mừng bạn đến thư viện.";
                return RedirectToAction("Index", "Shop");
            }
        }

        ViewBag.Error = error;
        return View();
    }

    [AllowAnonymous]
    public IActionResult Denied() => Content("Bạn không có quyền truy cập chức năng này.");
}
