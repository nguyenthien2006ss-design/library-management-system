using System.Text.Json;
using Microsoft.AspNetCore.Identity;

namespace LibraryManagement.Controllers;

/// <summary>Kho sách dành cho sinh viên: xem, thêm vào giỏ, mượn, theo dõi hồ sơ.</summary>
[Authorize]
public class ShopController : AppController
{
    private const string CartKey = "cart";
    private readonly AppDbContext _db;
    private readonly LibraryService _svc;
    private readonly UserManager<AppUser> _users;

    public ShopController(AppDbContext db, LibraryService svc, UserManager<AppUser> users)
    {
        _db = db; _svc = svc; _users = users;
    }

    public async Task<IActionResult> Index(string q, int? materialTypeId)
    {
        var query = _db.Books.Include(b => b.MaterialType).Include(b => b.Copies).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(b => b.Title.Contains(q) || b.Author.Contains(q) || b.Code.Contains(q) || b.Category.Contains(q));
        }
        if (materialTypeId is > 0) query = query.Where(b => b.MaterialTypeId == materialTypeId);

        ViewBag.Q = q;
        ViewBag.Selected = materialTypeId;
        ViewBag.MaterialTypes = await _db.MaterialTypes.OrderBy(t => t.Id).ToListAsync();
        return View(await query.OrderBy(b => b.Title).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var book = await _db.Books.Include(b => b.MaterialType).Include(b => b.Copies).FirstOrDefaultAsync(b => b.Id == id);
        if (book == null) return NotFound();
        ViewBag.InCart = GetCart();
        return View(book);
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart(int copyId)
    {
        var copy = await _db.BookCopies.Include(c => c.Book).FirstOrDefaultAsync(c => c.Id == copyId);
        if (copy == null) return NotFound();

        var code = $"{copy.Book.Code}-{copy.ControlNumber}";
        var cart = GetCart();
        if (cart.Contains(code)) TempData["Error"] = "Cuốn này đã có trong giỏ mượn.";
        else if (copy.Status != CopyStatus.Available) TempData["Error"] = $"Đầu sách {code} hiện không sẵn sàng.";
        else if (cart.Count >= LibraryService.MaxBooksPerLoan) TempData["Error"] = $"Giỏ mượn tối đa {LibraryService.MaxBooksPerLoan} cuốn.";
        else
        {
            cart.Add(code);
            SaveCart(cart);
            TempData["Success"] = $"Đã thêm “{copy.Book.Title}” vào giỏ mượn.";
        }
        return RedirectToAction(nameof(Details), new { id = copy.BookId });
    }

    [HttpPost]
    public IActionResult RemoveFromCart(string code)
    {
        var cart = GetCart();
        cart.Remove(code);
        SaveCart(cart);
        return RedirectToAction(nameof(Cart));
    }

    public async Task<IActionResult> Cart()
    {
        var items = new List<BookCopy>();
        foreach (var code in GetCart())
        {
            var c = await _svc.FindCopyByCodeAsync(code);
            if (c != null) items.Add(c);
        }
        return View(items);
    }

    [HttpPost, Authorize(Roles = "Student")]
    public async Task<IActionResult> Checkout()
    {
        var user = await _users.GetUserAsync(User);
        var card = user?.LibraryCardId == null ? null : await _db.LibraryCards.FindAsync(user.LibraryCardId.Value);
        if (card == null)
        {
            TempData["Error"] = "Tài khoản chưa liên kết thẻ thư viện.";
            return RedirectToAction(nameof(Cart));
        }

        var cart = GetCart();
        if (cart.Count == 0)
        {
            TempData["Error"] = "Giỏ mượn đang trống.";
            return RedirectToAction(nameof(Cart));
        }

        var err = await _svc.BorrowAsync(card.CardNumber, cart.ToArray(), CurrentUser);
        if (err != null)
        {
            TempData["Error"] = err;
            return RedirectToAction(nameof(Cart));
        }

        SaveCart(new List<string>());
        TempData["Success"] = "Mượn sách thành công! Vui lòng trả sách trong 15 ngày.";
        return RedirectToAction(nameof(My));
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> My()
    {
        var user = await _users.GetUserAsync(User);
        var card = user?.LibraryCardId == null ? null : await _db.LibraryCards.FindAsync(user.LibraryCardId.Value);
        if (card == null) return View((LibraryCard)null);

        ViewBag.Valid = LibraryService.IsCardValid(card, out var reason);
        ViewBag.Reason = reason;
        ViewBag.OpenLoans = await _db.Loans
            .Include(l => l.Items).ThenInclude(i => i.BookCopy).ThenInclude(c => c.Book)
            .Where(l => l.CardId == card.Id && l.Status == LoanStatus.Open)
            .OrderBy(l => l.DueDate).ToListAsync();
        ViewBag.History = await _db.Loans
            .Include(l => l.Items).ThenInclude(i => i.BookCopy).ThenInclude(c => c.Book)
            .Where(l => l.CardId == card.Id && l.Status == LoanStatus.Closed)
            .OrderByDescending(l => l.LoanDate).Take(20).ToListAsync();
        ViewBag.Incidents = await _db.Incidents
            .Include(i => i.BookCopy).ThenInclude(c => c.Book)
            .Where(i => i.CardId == card.Id)
            .OrderByDescending(i => i.ReportedDate).ToListAsync();
        return View(card);
    }

    private List<string> GetCart() =>
        HttpContext.Session.GetString(CartKey) is string s
            ? (JsonSerializer.Deserialize<List<string>>(s) ?? new List<string>())
            : new List<string>();

    private void SaveCart(List<string> cart) =>
        HttpContext.Session.SetString(CartKey, JsonSerializer.Serialize(cart));
}
