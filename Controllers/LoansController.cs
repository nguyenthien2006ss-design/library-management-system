namespace LibraryManagement.Controllers;

[Authorize(Roles = "Admin,Librarian")]
public class LoansController : AppController
{
    private readonly AppDbContext _db;
    private readonly LibraryService _svc;
    public LoansController(AppDbContext db, LibraryService svc) { _db = db; _svc = svc; }

    public async Task<IActionResult> Index()
    {
        var loans = await _db.Loans
            .Include(l => l.Card)
            .Include(l => l.Items).ThenInclude(i => i.BookCopy).ThenInclude(c => c.Book)
            .Where(l => l.Status == LoanStatus.Open)
            .OrderBy(l => l.DueDate)
            .ToListAsync();
        return View(loans);
    }

    public IActionResult Borrow() => View();

    // FR09, FR10
    [HttpPost]
    public async Task<IActionResult> Borrow(string cardNumber, string[] copyCodes)
    {
        var err = await _svc.BorrowAsync((cardNumber ?? "").Trim(), copyCodes, CurrentUser);
        if (err != null)
        {
            TempData["Error"] = err;
            return RedirectToAction(nameof(Borrow));
        }
        TempData["Success"] = "Mượn sách thành công. Hạn trả: 15 ngày kể từ hôm nay.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult ReturnBook() => View();

    // FR11
    [HttpPost]
    public async Task<IActionResult> ReturnBook(string copyCode)
    {
        var err = await _svc.ReturnBookAsync(copyCode ?? "", CurrentUser);
        if (err != null)
        {
            TempData["Error"] = err;
            return RedirectToAction(nameof(ReturnBook));
        }
        TempData["Success"] = "Đã ghi nhận trả sách.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult ReportIncident() => View();

    // FR12
    [HttpPost]
    public async Task<IActionResult> ReportIncident(string copyCode, IncidentType type, string note)
    {
        var err = await _svc.ReportIncidentAsync(copyCode ?? "", type, note, CurrentUser);
        if (err != null)
        {
            TempData["Error"] = err;
            return RedirectToAction(nameof(ReportIncident));
        }
        TempData["Success"] = "Đã ghi nhận sự cố. Thẻ của sinh viên đã bị khóa cho đến khi bồi thường.";
        return RedirectToAction(nameof(Index));
    }
}
