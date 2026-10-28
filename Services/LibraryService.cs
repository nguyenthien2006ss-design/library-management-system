namespace LibraryManagement.Services;

public record CountRow(string Label, int Count);
public record StatsResult(List<CountRow> ByBook, List<CountRow> ByStudent, List<CountRow> ByMonth);

/// <summary>Toàn bộ nghiệp vụ thư viện. Ràng buộc được kiểm tra tại đây (NFR01).</summary>
public class LibraryService
{
    public const int MaxBooksPerLoan = 3;  // FR09
    public const int LoanDays = 15;        // FR09

    private readonly AppDbContext _db;
    public LibraryService(AppDbContext db) => _db = db;

    // ---------- NFR02: nhật ký ----------
    public void Audit(string user, string action, string entity, string details) =>
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.Now,
            UserName = user,
            Action = action,
            Entity = entity,
            Details = details
        });

    // ---------- Sách & đầu sách (FR01 - FR05) ----------

    /// <summary>Thêm đầu sách mới, tự cấp số kiểm soát tiếp theo (001, 002, ...). Trả về lỗi hoặc null.</summary>
    public async Task<string> AddCopyAsync(int bookId, string user)
    {
        var book = await _db.Books.FindAsync(bookId);
        if (book == null) return "Không tìm thấy sách.";

        var numbers = await _db.BookCopies.Where(c => c.BookId == bookId).Select(c => c.ControlNumber).ToListAsync();
        var next = numbers.Select(n => int.TryParse(n, out var v) ? v : 0).DefaultIfEmpty(0).Max() + 1;
        var ctrl = next.ToString("000");

        _db.BookCopies.Add(new BookCopy { BookId = bookId, ControlNumber = ctrl, Status = CopyStatus.Available });
        Audit(user, "Thêm đầu sách", "BookCopy", $"{book.Code}-{ctrl}");
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>FR03: cập nhật còn / mất / hỏng kèm ghi chú. Trạng thái "cho mượn" chỉ do mượn/trả quyết định.</summary>
    public async Task<string> UpdateCopyStatusAsync(int copyId, CopyStatus status, string note, string user)
    {
        var copy = await _db.BookCopies.Include(c => c.Book).FirstOrDefaultAsync(c => c.Id == copyId);
        if (copy == null) return "Không tìm thấy đầu sách.";
        if (copy.Status == CopyStatus.OnLoan) return "Đầu sách đang cho mượn, hãy thực hiện qua chức năng Trả sách.";
        if (status == CopyStatus.OnLoan) return "Trạng thái 'Cho mượn' chỉ được tạo qua chức năng Mượn sách.";

        var old = copy.Status;
        copy.Status = status;
        copy.Note = note ?? "";
        Audit(user, "Đổi trạng thái đầu sách", "BookCopy",
            $"{copy.Book.Code}-{copy.ControlNumber}: {Labels.Copy(old)} → {Labels.Copy(status)}");
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>Tìm đầu sách theo mã dạng "MãSách-SốKiểmSoát", ví dụ S001-002.</summary>
    public async Task<BookCopy> FindCopyByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        code = code.Trim();
        var idx = code.LastIndexOf('-');
        if (idx <= 0 || idx == code.Length - 1) return null;

        var bookCode = code[..idx].Trim();
        var ctrl = code[(idx + 1)..].Trim();
        return await _db.BookCopies.Include(c => c.Book)
            .FirstOrDefaultAsync(c => c.Book.Code == bookCode && c.ControlNumber == ctrl);
    }

    // ---------- Thẻ thư viện (FR05 - FR08) ----------

    /// <summary>Thẻ dùng được khi: không bị khóa và chưa hết hạn.</summary>
    public static bool IsCardValid(LibraryCard card, out string reason)
    {
        if (!card.IsUsable)
        {
            reason = "Thẻ không sử dụng được";
            return false;
        }
        if (card.ExpiryDate.Date < DateTime.Today)
        {
            reason = "Thẻ đã hết hạn";
            return false;
        }
        reason = "";
        return true;
    }

    /// <summary>FR06: bật/tắt tình trạng sử dụng thẻ thủ công. Không mở lại thẻ khi còn sự cố chưa bồi thường.</summary>
    public async Task<string> ToggleCardAsync(int cardId, string user)
    {
        var card = await _db.LibraryCards.FindAsync(cardId);
        if (card == null) return "Không tìm thấy thẻ.";

        if (!card.IsUsable && await _db.Incidents.AnyAsync(i => i.CardId == cardId && i.CompensatedDate == null))
            return "Thẻ còn sự cố mất/hỏng chưa bồi thường nên chưa thể mở lại.";

        card.IsUsable = !card.IsUsable;
        Audit(user, card.IsUsable ? "Mở thẻ" : "Khóa thẻ", "LibraryCard", card.CardNumber);
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>FR08: ghi nhận bồi thường; thẻ được khôi phục khi không còn sự cố nào chưa bồi thường.</summary>
    public async Task<string> CompensateAsync(int incidentId, string user)
    {
        var inc = await _db.Incidents.Include(i => i.Card).FirstOrDefaultAsync(i => i.Id == incidentId);
        if (inc == null) return "Không tìm thấy sự cố.";
        if (inc.CompensatedDate != null) return "Sự cố này đã được bồi thường.";

        inc.CompensatedDate = DateTime.Now;
        var stillOpen = await _db.Incidents.AnyAsync(i => i.CardId == inc.CardId && i.CompensatedDate == null && i.Id != inc.Id);
        if (!stillOpen) inc.Card.IsUsable = true;

        Audit(user, "Ghi nhận bồi thường", "Incident", $"Thẻ {inc.Card.CardNumber}");
        await _db.SaveChangesAsync();
        return null;
    }

    // ---------- Mượn / trả / báo mất hỏng (FR09 - FR12) ----------

    /// <summary>Sinh viên còn đầu sách chưa trả trong bất kỳ phiếu mượn nào thì coi là còn phiếu đang mở.</summary>
    public Task<bool> HasOpenLoanAsync(int cardId) =>
        _db.LoanItems.AnyAsync(i => i.Loan.CardId == cardId && i.ReturnDate == null);

    public async Task<string> BorrowAsync(string cardNumber, string[] codes, string user)
    {
        var card = await _db.LibraryCards.FirstOrDefaultAsync(c => c.CardNumber == cardNumber);
        if (card == null) return "Không tìm thấy thẻ thư viện.";
        if (!IsCardValid(card, out var reason)) return reason;

        var list = (codes ?? Array.Empty<string>())
            .Select(c => (c ?? "").Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (list.Count == 0) return "Vui lòng nhập ít nhất một mã sách.";
        if (list.Count > MaxBooksPerLoan) return $"Mỗi lượt mượn tối đa {MaxBooksPerLoan} mã sách.";           // FR09
        if (await HasOpenLoanAsync(card.Id))
            return "Sinh viên còn phiếu mượn đang mở (chưa trả hết sách). Hãy trả hết trước khi mượn tiếp.";  // FR10

        var copies = new List<BookCopy>();
        foreach (var code in list)
        {
            var copy = await FindCopyByCodeAsync(code);
            if (copy == null) return $"Không tìm thấy đầu sách mã '{code}'.";
            if (copy.Status != CopyStatus.Available)
                return $"Đầu sách '{code}' đang ở trạng thái: {Labels.Copy(copy.Status)}.";
            copies.Add(copy);
        }

        var today = DateTime.Today;
        var loan = new Loan
        {
            CardId = card.Id,
            LoanDate = today,
            DueDate = today.AddDays(LoanDays),   // FR09: tối đa 15 ngày
            Status = LoanStatus.Open
        };
        foreach (var copy in copies)
        {
            copy.Status = CopyStatus.OnLoan;     // FR04
            loan.Items.Add(new LoanItem { BookCopy = copy });
        }

        _db.Loans.Add(loan);
        Audit(user, "Mượn sách", "Loan", $"Thẻ {card.CardNumber}: {string.Join(", ", list)} (hạn {loan.DueDate:dd/MM/yyyy})");
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string> ReturnBookAsync(string copyCode, string user)
    {
        var copy = await FindCopyByCodeAsync(copyCode);
        if (copy == null) return "Không tìm thấy mã sách.";

        var item = await _db.LoanItems.Include(i => i.Loan)
            .FirstOrDefaultAsync(i => i.BookCopyId == copy.Id && i.ReturnDate == null);
        if (item == null) return "Đầu sách này không nằm trong phiếu mượn đang mở.";

        item.ReturnDate = DateTime.Now;
        copy.Status = CopyStatus.Available;                  // FR11, FR04
        await RefreshLoanStatusAsync(item.LoanId);

        var overdue = DateTime.Today > item.Loan.DueDate.Date ? " - trả quá hạn" : "";
        Audit(user, "Trả sách", "LoanItem", $"{copyCode} (phiếu #{item.LoanId}){overdue}");
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>FR12: báo mất/hỏng trong thời gian mượn. Tự động khóa thẻ (FR07).</summary>
    public async Task<string> ReportIncidentAsync(string copyCode, IncidentType type, string note, string user)
    {
        var copy = await FindCopyByCodeAsync(copyCode);
        if (copy == null) return "Không tìm thấy mã sách.";

        var item = await _db.LoanItems.Include(i => i.Loan)
            .FirstOrDefaultAsync(i => i.BookCopyId == copy.Id && i.ReturnDate == null);
        if (item == null) return "Đầu sách này không đang được mượn nên không thể báo mất/hỏng.";

        var card = await _db.LibraryCards.FindAsync(item.Loan.CardId);

        item.ReturnDate = DateTime.Now;   // sách không còn ở tay sinh viên dưới dạng đang mượn
        copy.Status = type == IncidentType.Lost ? CopyStatus.Lost : CopyStatus.Damaged;   // FR04
        copy.Note = note ?? "";
        card.IsUsable = false;            // FR07: khóa thẻ đến khi bồi thường

        _db.Incidents.Add(new Incident
        {
            CardId = card.Id,
            BookCopyId = copy.Id,
            LoanId = item.LoanId,
            Type = type,
            ReportedDate = DateTime.Now,
            Note = note ?? ""
        });

        await RefreshLoanStatusAsync(item.LoanId);
        Audit(user, "Báo " + Labels.Incident(type).ToLower() + " sách", "Incident",
            $"{copyCode} - thẻ {card.CardNumber}");
        await _db.SaveChangesAsync();
        return null;
    }

    private async Task RefreshLoanStatusAsync(int loanId)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        var items = await _db.LoanItems.Where(i => i.LoanId == loanId).ToListAsync();
        loan.Status = items.Any(i => i.ReturnDate == null) ? LoanStatus.Open : LoanStatus.Closed;
    }

    // ---------- Báo cáo & thống kê (FR13, FR14) ----------

    public Task<List<Incident>> GetIncidentsByYearAsync(int year)
    {
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);
        return _db.Incidents
            .Include(i => i.Card)
            .Include(i => i.BookCopy).ThenInclude(c => c.Book)
            .Where(i => i.ReportedDate >= start && i.ReportedDate < end)
            .OrderBy(i => i.ReportedDate)
            .ToListAsync();
    }

    public async Task<StatsResult> GetStatsAsync(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date.AddDays(1);

        var items = await _db.LoanItems
            .Include(i => i.Loan).ThenInclude(l => l.Card)
            .Include(i => i.BookCopy).ThenInclude(c => c.Book)
            .Where(i => i.Loan.LoanDate >= start && i.Loan.LoanDate < end)
            .ToListAsync();

        var byBook = items.GroupBy(i => $"{i.BookCopy.Book.Code} - {i.BookCopy.Book.Title}")
            .Select(g => new CountRow(g.Key, g.Count())).OrderByDescending(r => r.Count).ToList();

        var byStudent = items.GroupBy(i => $"{i.Loan.Card.CardNumber} - {i.Loan.Card.StudentName}")
            .Select(g => new CountRow(g.Key, g.Count())).OrderByDescending(r => r.Count).ToList();

        var byMonth = items.GroupBy(i => i.Loan.LoanDate.ToString("MM/yyyy"))
            .OrderBy(g => g.Key)
            .Select(g => new CountRow(g.Key, g.Count())).ToList();

        return new StatsResult(byBook, byStudent, byMonth);
    }
}
