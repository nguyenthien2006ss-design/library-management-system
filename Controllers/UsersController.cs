using Microsoft.AspNetCore.Identity;

namespace LibraryManagement.Controllers;

// FR15, FR16: admin tạo tài khoản và phân quyền
[Authorize(Roles = "Admin")]
public class UsersController : AppController
{
    private static readonly string[] Roles = { "Admin", "Librarian" };
    private readonly UserManager<AppUser> _users;
    public UsersController(UserManager<AppUser> users) => _users = users;

    public async Task<IActionResult> Index()
    {
        var rows = new List<UserRow>();
        foreach (var u in _users.Users.OrderBy(x => x.UserName).ToList())
        {
            var roles = await _users.GetRolesAsync(u);
            rows.Add(new UserRow(u, roles.FirstOrDefault() ?? "-"));
        }
        return View(rows);
    }

    public IActionResult Create() => View();

    [HttpPost]
    public async Task<IActionResult> Create(string userName, string fullName, string password, string role)
    {
        if (!Roles.Contains(role)) role = "Librarian";
        var user = new AppUser { UserName = (userName ?? "").Trim(), FullName = fullName ?? "", Email = $"{userName}@library.local" };
        var result = await _users.CreateAsync(user, password ?? "");
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Create));
        }
        await _users.AddToRoleAsync(user, role);
        TempData["Success"] = $"Đã tạo tài khoản {user.UserName}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetRole(string userId, string role)
    {
        if (!Roles.Contains(role)) return RedirectToAction(nameof(Index));
        var user = await _users.FindByIdAsync(userId);
        if (user == null) return NotFound();

        await _users.RemoveFromRolesAsync(user, await _users.GetRolesAsync(user));
        await _users.AddToRoleAsync(user, role);
        TempData["Success"] = $"Đã phân quyền {user.UserName} → {role}.";
        return RedirectToAction(nameof(Index));
    }
}
