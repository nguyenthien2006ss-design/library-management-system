using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace LibraryManagement.Controllers;

/// <summary>Tạo ảnh bìa minh họa (SVG) cho từng cuốn sách theo tiêu đề và màu bìa.</summary>
public class CoverController : Controller
{
    private readonly AppDbContext _db;
    public CoverController(AppDbContext db) => _db = db;

    [AllowAnonymous]
    [HttpGet("cover/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var b = await _db.Books.FindAsync(id);
        if (b == null) return NotFound();

        var color = Regex.IsMatch(b.CoverColor ?? "", "^#[0-9a-fA-F]{6}$") ? b.CoverColor : "#2563eb";
        var lines = Wrap(b.Title, 15).Take(4).ToList();
        int startY = 200 - (lines.Count - 1) * 16;
        int seed = Math.Abs(b.Id * 37 + (b.Code ?? "").Length * 11);

        var sb = new StringBuilder();
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 300 400'>");
        sb.Append($"<defs><linearGradient id='g' x1='0' y1='0' x2='1' y2='1'><stop offset='0' stop-color='{color}'/><stop offset='1' stop-color='#0f172a'/></linearGradient></defs>");
        sb.Append("<rect width='300' height='400' fill='url(#g)'/>");
        sb.Append($"<circle cx='{60 + seed % 180}' cy='{110 + seed % 90}' r='{50 + seed % 30}' fill='white' opacity='0.12'/>");
        sb.Append($"<circle cx='{220 - seed % 120}' cy='{300 + seed % 40}' r='70' fill='white' opacity='0.08'/>");
        sb.Append("<path d='M40 330 Q150 290 260 330' stroke='white' stroke-opacity='0.3' fill='none' stroke-width='2'/>");
        sb.Append("<rect x='22' y='22' width='256' height='356' rx='12' fill='none' stroke='white' stroke-opacity='0.35'/>");
        sb.Append($"<text x='150' y='68' text-anchor='middle' font-family='Georgia,serif' font-size='13' fill='white' opacity='0.85' letter-spacing='3'>{SecurityElement.Escape(b.Code)}</text>");
        for (int i = 0; i < lines.Count; i++)
        {
            sb.Append($"<text x='150' y='{startY + i * 32}' text-anchor='middle' font-family='Georgia,serif' font-size='24' font-weight='bold' fill='white'>{SecurityElement.Escape(lines[i])}</text>");
        }
        sb.Append($"<text x='150' y='362' text-anchor='middle' font-family='Segoe UI,Arial,sans-serif' font-size='14' fill='white' opacity='0.9'>{SecurityElement.Escape(b.Author)}</text>");
        sb.Append("</svg>");
        return Content(sb.ToString(), "image/svg+xml; charset=utf-8");
    }

    private static List<string> Wrap(string text, int max)
    {
        var lines = new List<string>();
        var cur = "";
        foreach (var w in (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (cur.Length + w.Length + 1 > max && cur.Length > 0) { lines.Add(cur); cur = w; }
            else cur = cur.Length == 0 ? w : cur + " " + w;
        }
        if (cur.Length > 0) lines.Add(cur);
        return lines;
    }
}
