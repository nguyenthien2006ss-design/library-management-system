namespace LibraryManagement.Controllers;

[Authorize(Roles = "Admin,Librarian")]
public class BooksController : AppController
{
    private readonly AppDbContext _db;
    private readonly LibraryService _svc;
    public BooksController(AppDbContext db, LibraryService svc) { _db = db; _svc = svc; }

    // FR14 / NFR03: tìm nhanh (không phân biệt hoa thường - PBI03)
    public async Task<IActionResult> Index(string q, int? materialTypeId)
    {
        var query = _db.Books.Include(b => b.MaterialType).Include(b => b.Copies).AsQueryable();
        if (materialTypeId is > 0) query = query.Where(b => b.MaterialTypeId == materialTypeId);

        var books = await query.OrderBy(b => b.Title).ToListAsync();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            var k = q.ToLowerInvariant();
            books = books.Where(b =>
                (b.Code ?? "").ToLowerInvariant().Contains(k) ||
                (b.Title ?? "").ToLowerInvariant().Contains(k) ||
                (b.Author ?? "").ToLowerInvariant().Contains(k) ||
                (b.Category ?? "").ToLowerInvariant().Contains(k)).ToList();
        }

        ViewBag.Q = q;
        ViewBag.Selected = materialTypeId;
        ViewBag.MaterialTypes = await _db.MaterialTypes.OrderBy(t => t.Id).ToListAsync();
        return View(books);
    }

    public async Task<IActionResult> Create()
    {
        await LoadTypes();
        return View("Form", new Book { PublishYear = DateTime.Today.Year });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var book = await _db.Books.FindAsync(id);
        if (book == null) return NotFound();
        await LoadTypes();
        return View("Form", book);
    }

    [HttpPost]
    public async Task<IActionResult> Save(Book book)
    {
        book.Code = book.Code?.Trim() ?? "";
        book.Title = book.Title?.Trim() ?? "";
        book.Author = book.Author?.Trim() ?? "";
        if (await _db.Books.AnyAsync(b => b.Code == book.Code && b.Id != book.Id))
            ModelState.AddModelError(nameof(Book.Code), "Mã sách đã tồn tại.");
        if (!ModelState.IsValid)
        {
            await LoadTypes();
            return View("Form", book);
        }

        if (book.Id == 0)
        {
            _db.Books.Add(book);
            _svc.Audit(CurrentUser, "Thêm sách", "Book", book.Code);
            TempData["Success"] = "Đã thêm sách.";
        }
        else
        {
            var existing = await _db.Books.FindAsync(book.Id);
            if (existing == null) return NotFound();
            existing.Code = book.Code;
            existing.Title = book.Title;
            existing.Author = book.Author;
            existing.Category = book.Category;
            existing.PublishYear = book.PublishYear;
            existing.Publisher = book.Publisher;
            existing.MaterialTypeId = book.MaterialTypeId;
            existing.Description = book.Description;
            existing.CoverColor = book.CoverColor;
            existing.CoverUrl = book.CoverUrl;
            _svc.Audit(CurrentUser, "Sửa sách", "Book", book.Code);
            TempData["Success"] = "Đã cập nhật sách.";
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var book = await _db.Books.Include(b => b.MaterialType).Include(b => b.Copies).FirstOrDefaultAsync(b => b.Id == id);
        if (book == null) return NotFound();
        return View(book);
    }

    // FR02: thêm bản sao (số kiểm soát tự cấp)
    [HttpPost]
    public async Task<IActionResult> AddCopy(int bookId)
    {
        var err = await _svc.AddCopyAsync(bookId, CurrentUser);
        if (err != null) TempData["Error"] = err; else TempData["Success"] = "Đã thêm bản sao mới.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    // FR03: cập nhật tình trạng đầu sách
    [HttpPost]
    public async Task<IActionResult> UpdateCopy(int bookId, int copyId, CopyStatus status, string note)
    {
        var err = await _svc.UpdateCopyStatusAsync(copyId, status, note, CurrentUser);
        if (err != null) TempData["Error"] = err; else TempData["Success"] = "Đã cập nhật tình trạng đầu sách.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var book = await _db.Books.Include(b => b.Copies).FirstOrDefaultAsync(b => b.Id == id);
        if (book == null) return NotFound();

        var used = await _db.LoanItems.AnyAsync(i => i.BookCopy.BookId == id)
                   || await _db.Incidents.AnyAsync(i => i.BookCopy.BookId == id);
        if (used)
        {
            TempData["Error"] = "Không thể xóa: sách đã có lịch sử mượn hoặc báo mất/hỏng.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.BookCopies.RemoveRange(book.Copies);
        _db.Books.Remove(book);
        _svc.Audit(CurrentUser, "Xóa sách", "Book", book.Code);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã xóa sách.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadTypes() =>
        ViewBag.MaterialTypes = await _db.MaterialTypes.OrderBy(t => t.Id).ToListAsync();
}
