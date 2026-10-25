namespace LibraryManagement.Controllers;

[Authorize(Roles = "Admin,Librarian")]
public class CardsController : AppController
{
    private readonly AppDbContext _db;
    private readonly LibraryService _svc;
    public CardsController(AppDbContext db, LibraryService svc) { _db = db; _svc = svc; }

    public async Task<IActionResult> Index(string q)
    {
        var query = _db.LibraryCards.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(c => c.CardNumber.Contains(q) || c.StudentName.Contains(q));
        }
        ViewBag.Q = q;
        return View(await query.OrderBy(c => c.CardNumber).ToListAsync());
    }

    public IActionResult Create() => View(new LibraryCard());

    // FR05: tạo thẻ
    [HttpPost]
    public async Task<IActionResult> Create(LibraryCard card)
    {
        if (await _db.LibraryCards.AnyAsync(c => c.CardNumber == card.CardNumber))
            ModelState.AddModelError(nameof(LibraryCard.CardNumber), "Số thẻ đã tồn tại.");
        if (card.ExpiryDate.Date < DateTime.Today)
            ModelState.AddModelError(nameof(LibraryCard.ExpiryDate), "Ngày hết hạn phải từ hôm nay trở đi.");
        if (!ModelState.IsValid) return View(card);

        _db.LibraryCards.Add(card);
        _svc.Audit(CurrentUser, "Cấp thẻ", "LibraryCard", card.CardNumber);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cấp thẻ thư viện.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var card = await _db.LibraryCards.FindAsync(id);
        if (card == null) return NotFound();

        ViewBag.Incidents = await _db.Incidents
            .Include(i => i.BookCopy).ThenInclude(c => c.Book)
            .Where(i => i.CardId == id)
            .OrderByDescending(i => i.ReportedDate).ToListAsync();

        ViewBag.Loans = await _db.Loans
            .Include(l => l.Items).ThenInclude(i => i.BookCopy).ThenInclude(c => c.Book)
            .Where(l => l.CardId == id && l.Status == LoanStatus.Open)
            .ToListAsync();
        return View(card);
    }

    // FR06: đổi tình trạng sử dụng thẻ
    [HttpPost]
    public async Task<IActionResult> ToggleUsable(int id)
    {
        var err = await _svc.ToggleCardAsync(id, CurrentUser);
        if (err != null) TempData["Error"] = err; else TempData["Success"] = "Đã cập nhật tình trạng thẻ.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // FR08: ghi nhận bồi thường
    [HttpPost]
    public async Task<IActionResult> Compensate(int incidentId, int cardId)
    {
        var err = await _svc.CompensateAsync(incidentId, CurrentUser);
        if (err != null) TempData["Error"] = err; else TempData["Success"] = "Đã ghi nhận bồi thường và cập nhật thẻ.";
        return RedirectToAction(nameof(Details), new { id = cardId });
    }
}
