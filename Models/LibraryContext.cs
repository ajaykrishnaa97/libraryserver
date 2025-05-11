using Microsoft.EntityFrameworkCore;

namespace Library.Models
{
    public class LibraryContext : DbContext
    {
        public LibraryContext(DbContextOptions<LibraryContext> options) : base(options) { }

        public DbSet<LibraryBranch> LibraryBranches { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<BookCopy> BookCopies { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasOne(u => u.LibraryBranch)
                .WithMany() 
                .HasForeignKey(u => u.LibraryId)
                .IsRequired();

            modelBuilder.Entity<User>().Ignore("LibraryBranchId");
            modelBuilder.Entity<User>().Ignore("MembersLocation");
        }
    }
}
