using System.Text;

namespace LibraryManagement.Controllers;

[Authorize(Roles = "Admin,Librarian")]
public class ReportsController : AppController
{
    private readonly LibraryService _svc;
    public ReportsController(LibraryService svc) => _svc = svc;

    // FR13: báo cáo hàng năm sách mất/hỏng để thanh lý
    public async Task<IActionResult> Liquidation(int? year)
    {
        var y = year ?? DateTime.Today.Year;
        ViewBag.Year = y;
        return View(await _svc.GetIncidentsByYearAsync(y));
    }

    public async Task<IActionResult> LiquidationCsv(int year)
    {
        var list = await _svc.GetIncidentsByYearAsync(year);
        var sb = new StringBuilder();
        sb.AppendLine("STT,Ngày báo,Loại,Mã sách,Tên sách,Số kiểm soát,Số thẻ,Sinh viên,Ghi chú,Đã bồi thường");
        int stt = 1;
        foreach (var x in list)
        {
            sb.AppendLine(string.Join(",",
                stt++,
                x.ReportedDate.ToString("dd/MM/yyyy"),
                Labels.Incident(x.Type),
                Csv(x.BookCopy.Book.Code),
                Csv(x.BookCopy.Book.Title),
                Csv(x.BookCopy.ControlNumber),
                Csv(x.Card.CardNumber),
                Csv(x.Card.StudentName),
                Csv(x.Note),
                x.CompensatedDate == null ? "Chưa" : "Đã"));
        }
        // BOM giúp Excel đọc đúng tiếng Việt
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"sach_mat_hong_{year}.csv");
    }

    // FR14: thống kê theo sách, sinh viên, thời gian
    public async Task<IActionResult> Stats(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var f = from ?? new DateTime(today.Year, today.Month, 1);
        var t = to ?? today;
        ViewBag.From = f.ToString("yyyy-MM-dd");
        ViewBag.To = t.ToString("yyyy-MM-dd");
        return View(await _svc.GetStatsAsync(f, t));
    }

    private static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
}
