using LibraryManagement.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace LibraryManagement.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MaterialType> MaterialTypes { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<BookCopy> BookCopies { get; set; }
    public DbSet<LibraryCard> LibraryCards { get; set; }
    public DbSet<Loan> Loans { get; set; }
    public DbSet<LoanItem> LoanItems { get; set; }
    public DbSet<Incident> Incidents { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Book>().HasIndex(x => x.Code).IsUnique();
        b.Entity<BookCopy>().HasIndex(x => new { x.BookId, x.ControlNumber }).IsUnique();
        b.Entity<LibraryCard>().HasIndex(x => x.CardNumber).IsUnique();

        // Lịch sử mượn/trả và sự cố luôn được giữ lại: không xóa dây chuyền
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
