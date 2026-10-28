using LibraryManagement.Models;
using Microsoft.AspNetCore.Identity;

namespace LibraryManagement.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var users = sp.GetRequiredService<UserManager<AppUser>>();

        foreach (var r in new[] { "Admin", "Librarian", "Student" })
            if (!await roles.RoleExistsAsync(r))
                await roles.CreateAsync(new IdentityRole(r));

        // Tài khoản nhân viên demo
        await CreateStaff(users, "admin", "Quản trị viên", "admin123", "Admin");
        await CreateStaff(users, "thuthu", "Thủ thư", "thuthu123", "Librarian");

        if (!db.MaterialTypes.Any())
        {
            db.MaterialTypes.AddRange(
                new MaterialType { Name = "Sách" },
                new MaterialType { Name = "Báo" },
                new MaterialType { Name = "Tạp chí" });
            await db.SaveChangesAsync();
        }

        if (!db.Books.Any())
        {
            var sach = db.MaterialTypes.First(t => t.Name == "Sách").Id;
            var bao = db.MaterialTypes.First(t => t.Name == "Báo").Id;
            var tapchi = db.MaterialTypes.First(t => t.Name == "Tạp chí").Id;

            db.Books.AddRange(
                MakeBook("S001", "Nhập môn Công nghệ phần mềm", "Ian Sommerville", "Công nghệ phần mềm", 2016, "Pearson", sach, "#2563eb",
                    "Giới thiệu các khái niệm nền tảng: yêu cầu, thiết kế, kiểm thử và quy trình phát triển phần mềm.", 2),
                MakeBook("S002", "Cấu trúc dữ liệu và giải thuật", "Nguyễn Đức Nghĩa", "Tin học", 2010, "NXB Giáo dục", sach, "#059669",
                    "Các cấu trúc dữ liệu cơ bản như danh sách, cây, đồ thị và cách phân tích độ phức tạp thuật toán.", 1),
                MakeBook("S003", "Clean Code", "Robert C. Martin", "Lập trình", 2008, "Prentice Hall", sach, "#0f766e",
                    "Nguyên tắc viết mã dễ đọc, dễ bảo trì và cách đặt tên, hàm, lớp cho gọn gàng.", 3),
                MakeBook("S004", "Đắc nhân tâm", "Dale Carnegie", "Kỹ năng sống", 2019, "NXB Tổng hợp", sach, "#dc2626",
                    "Những nguyên tắc ứng xử giúp xây dựng mối quan hệ và tăng sức thuyết phục trong giao tiếp.", 2),
                MakeBook("S005", "Lược sử thời gian", "Stephen Hawking", "Khoa học", 1988, "Bantam Books", sach, "#7c3aed",
                    "Hành trình khám phá về vũ trụ, Big Bang, hố đen và bản chất của thời gian.", 1),
                MakeBook("S006", "Nhà giả kim", "Paulo Coelho", "Tiểu thuyết", 1988, "NXB Hội Nhà văn", sach, "#d97706",
                    "Câu chuyện về một chàng chăn cừu đi tìm kho báu và học được nhiều điều về ước mơ.", 2),
                MakeBook("S007", "Design Patterns", "Gamma, Helm, Johnson, Vlissides", "Lập trình", 1994, "Addison-Wesley", sach, "#1e293b",
                    "Hai mươi ba mẫu thiết kế hướng đối tượng kinh điển để giải quyết các bài toán lặp lại.", 1),
                MakeBook("S008", "Tuổi trẻ đáng giá bao nhiêu", "Rosie Nguyễn", "Kỹ năng sống", 2016, "NXB Hội Nhà văn", sach, "#db2777",
                    "Những suy ngẫm về tuổi trẻ, sự trưởng thành và cách sống có mục tiêu.", 2),
                MakeBook("S009", "Sapiens: Lược sử loài người", "Yuval Noah Harari", "Lịch sử", 2014, "Harper", sach, "#92400e",
                    "Nhìn lại quá trình loài người phát triển từ thời săn bắt hái lượm đến xã hội hiện đại.", 1),
                MakeBook("B001", "Báo Tuổi Trẻ - Số tuần", "Tòa soạn", "Thời sự", 2026, "Tuổi Trẻ", bao, "#475569",
                    "Tin tức tổng hợp trong tuần, cập nhật thời sự trong nước và quốc tế.", 2),
                MakeBook("T001", "Tạp chí Khoa học & Công nghệ", "Nhiều tác giả", "Khoa học", 2026, "Viện Khoa học", tapchi, "#0369a1",
                    "Các bài viết về nghiên cứu, đổi mới sáng tạo và xu hướng công nghệ mới.", 2));
            await db.SaveChangesAsync();
        }

        if (!db.LibraryCards.Any())
        {
            db.LibraryCards.AddRange(
                new LibraryCard { CardNumber = "SV2026001", StudentName = "Nguyễn Văn An", ExpiryDate = DateTime.Today.AddYears(2) },
                new LibraryCard { CardNumber = "SV2026002", StudentName = "Trần Thị Bình", ExpiryDate = DateTime.Today.AddYears(2) },
                new LibraryCard { CardNumber = "SV2026003", StudentName = "Lê Minh Châu", ExpiryDate = DateTime.Today.AddYears(2) },
                new LibraryCard { CardNumber = "SV2026004", StudentName = "Phạm Quốc Dũng", ExpiryDate = DateTime.Today.AddDays(-10) });
            await db.SaveChangesAsync();
        }

        // Tài khoản sinh viên demo (mật khẩu: sinhvien123)
        await CreateStudent(users, db, "sv001", "SV2026001", "sinhvien123");
        await CreateStudent(users, db, "sv002", "SV2026002", "sinhvien123");

        // Một phiếu mượn đang mở để minh họa
        if (!db.Loans.Any())
        {
            var card = db.LibraryCards.First(c => c.CardNumber == "SV2026002");
            var copy = db.BookCopies.Include(c => c.Book).First(c => c.Book.Code == "S002" && c.ControlNumber == "001");
            copy.Status = CopyStatus.OnLoan;
            db.Loans.Add(new Loan
            {
                CardId = card.Id,
                LoanDate = DateTime.Today.AddDays(-6),
                DueDate = DateTime.Today.AddDays(9),
                Status = LoanStatus.Open,
                Items = { new LoanItem { BookCopy = copy } }
            });
            await db.SaveChangesAsync();
        }
    }

    private static Book MakeBook(string code, string title, string author, string category, int year, string publisher,
        int typeId, string color, string description, int copies) => new Book
    {
        Code = code, Title = title, Author = author, Category = category, PublishYear = year,
        Publisher = publisher, MaterialTypeId = typeId, CoverColor = color, Description = description,
        Copies = MakeCopies(copies)
    };

    private static List<BookCopy> MakeCopies(int n) =>
        Enumerable.Range(1, n).Select(i => new BookCopy { ControlNumber = i.ToString("000") }).ToList();

    private static async Task CreateStaff(UserManager<AppUser> users, string userName, string fullName, string password, string role)
    {
        if (await users.FindByNameAsync(userName) != null) return;
        var u = new AppUser { UserName = userName, FullName = fullName, Email = $"{userName}@library.local" };
        var res = await users.CreateAsync(u, password);
        if (res.Succeeded) await users.AddToRoleAsync(u, role);
    }

    private static async Task CreateStudent(UserManager<AppUser> users, AppDbContext db, string userName, string cardNumber, string password)
    {
        if (await users.FindByNameAsync(userName) != null) return;
        var card = db.LibraryCards.First(c => c.CardNumber == cardNumber);
        var u = new AppUser { UserName = userName, FullName = card.StudentName, Email = $"{userName}@student.library.local", LibraryCardId = card.Id };
        var res = await users.CreateAsync(u, password);
        if (res.Succeeded) await users.AddToRoleAsync(u, "Student");
    }
}
