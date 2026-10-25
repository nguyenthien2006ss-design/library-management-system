using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace LibraryManagement.Models;

public enum CopyStatus { Available = 0, OnLoan = 1, Lost = 2, Damaged = 3 }
public enum LoanStatus { Open = 0, Closed = 1 }
public enum IncidentType { Lost = 0, Damaged = 1 }

public static class Labels
{
    public static string Copy(CopyStatus s) => s switch
    {
        CopyStatus.Available => "Còn",
        CopyStatus.OnLoan => "Cho mượn",
        CopyStatus.Lost => "Mất",
        CopyStatus.Damaged => "Hỏng",
        _ => ""
    };

    public static string Incident(IncidentType t) => t == IncidentType.Lost ? "Mất" : "Hỏng";
}

public class AppUser : IdentityUser
{
    [StringLength(100)] public string FullName { get; set; } = "";
    public int? LibraryCardId { get; set; }   // tài khoản sinh viên liên kết với thẻ
}

public record UserRow(AppUser User, string Role);

// NFR04: loại tài liệu có thể mở rộng (Sách, Báo, Tạp chí...) mà không cần sửa code
public class MaterialType
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string Name { get; set; } = "";
}

// FR01: thông tin sách
public class Book
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã sách"), StringLength(20)]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tên sách"), StringLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tác giả"), StringLength(150)]
    public string Author { get; set; } = "";

    [StringLength(80)]
    public string Category { get; set; } = "";

    [Range(1000, 2100, ErrorMessage = "Năm xuất bản không hợp lệ")]
    public int PublishYear { get; set; }

    [StringLength(150)]
    public string Publisher { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại tài liệu")]
    public int MaterialTypeId { get; set; }
    public MaterialType MaterialType { get; set; }

    public List<BookCopy> Copies { get; set; } = new();

    [StringLength(1000)] public string Description { get; set; } = "";
    [StringLength(7)] public string CoverColor { get; set; } = "#2563eb";   // màu bìa minh họa
    [StringLength(300)] public string CoverUrl { get; set; } = "";         // ảnh bìa thật (tùy chọn)
}

// FR02, FR03: đầu sách (bản sao vật lý) phân biệt bằng số kiểm soát
public class BookCopy
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public Book Book { get; set; }

    [Required, StringLength(10)]
    public string ControlNumber { get; set; } = "";

    public CopyStatus Status { get; set; } = CopyStatus.Available;

    [StringLength(300)]
    public string Note { get; set; } = "";
}

// FR05, FR06: thẻ thư viện
public class LibraryCard
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số thẻ"), StringLength(20)]
    public string CardNumber { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập họ tên sinh viên"), StringLength(120)]
    public string StudentName { get; set; } = "";

    [DataType(DataType.Date)]
    public DateTime ExpiryDate { get; set; } = DateTime.Today.AddYears(4);

    // true = sử dụng được; false = không sử dụng được (khóa thủ công hoặc tự động do mất/hỏng sách)
    public bool IsUsable { get; set; } = true;
}

// FR09, FR11: phiếu mượn
public class Loan
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public LibraryCard Card { get; set; }
    public DateTime LoanDate { get; set; }
    public DateTime DueDate { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Open;
    public List<LoanItem> Items { get; set; } = new();
}

public class LoanItem
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public Loan Loan { get; set; }
    public int BookCopyId { get; set; }
    public BookCopy BookCopy { get; set; }
    public DateTime? ReturnDate { get; set; }   // null = chưa trả
}

// FR07, FR08, FR12, FR13: sự cố mất/hỏng và bồi thường
public class Incident
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public LibraryCard Card { get; set; }
    public int BookCopyId { get; set; }
    public BookCopy BookCopy { get; set; }
    public int LoanId { get; set; }
    public IncidentType Type { get; set; }
    public DateTime ReportedDate { get; set; }
    public string Note { get; set; } = "";
    public DateTime? CompensatedDate { get; set; }
}

// NFR02: nhật ký mượn/trả và mọi thao tác quan trọng
public class AuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string Details { get; set; } = "";
}
