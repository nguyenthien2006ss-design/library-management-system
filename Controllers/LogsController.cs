namespace LibraryManagement.Controllers;

[Authorize(Roles = "Admin,Librarian")]
public class LogsController : AppController
{
    private readonly AppDbContext _db;
    public LogsController(AppDbContext db) => _db = db;

    // NFR02: tra cứu nhật ký
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var start = (from ?? DateTime.Today.AddDays(-30)).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);
        ViewBag.From = start.ToString("yyyy-MM-dd");
        ViewBag.To = end.AddDays(-1).ToString("yyyy-MM-dd");

        var logs = await _db.AuditLogs
            .Where(a => a.Timestamp >= start && a.Timestamp < end)
            .OrderByDescending(a => a.Timestamp)
            .Take(500)
            .ToListAsync();
        return View(logs);
    }
}
