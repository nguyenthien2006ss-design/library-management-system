namespace LibraryManagement.Controllers;

[Authorize]
public class HomeController : AppController
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Student")) return RedirectToAction("Index", "Shop");

        ViewBag.Titles = await _db.Books.CountAsync();
        ViewBag.Copies = await _db.BookCopies.CountAsync();
        ViewBag.Available = await _db.BookCopies.CountAsync(c => c.Status == CopyStatus.Available);
        ViewBag.OpenLoans = await _db.Loans.CountAsync(l => l.Status == LoanStatus.Open);
        ViewBag.Overdue = await _db.LoanItems.CountAsync(i => i.ReturnDate == null && i.Loan.DueDate < DateTime.Today);
        ViewBag.Locked = await _db.LibraryCards.CountAsync(c => !c.IsUsable);
        ViewBag.Pending = await _db.Incidents.CountAsync(i => i.CompensatedDate == null);
        ViewBag.Students = await _db.Users.CountAsync(u => u.LibraryCardId != null);
        ViewBag.Recent = await _db.Loans
            .Include(l => l.Card)
            .Include(l => l.Items).ThenInclude(i => i.BookCopy).ThenInclude(c => c.Book)
            .Where(l => l.Status == LoanStatus.Open)
            .OrderBy(l => l.DueDate).Take(6).ToListAsync();
        return View();
    }

    [AllowAnonymous]
    public IActionResult Error() => Content("Đã xảy ra lỗi. Vui lòng thử lại.");
}
