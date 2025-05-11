using System.ComponentModel.DataAnnotations.Schema;

namespace Library.Models
{
    [Table("user_reservations")]
    public class Reservation
    {
        public int Id { get; set; }

        public int BookCopyId { get; set; }
        public BookCopy BookCopy { get; set; }  // navigation

        public int UserId { get; set; }  
        public User User { get; set; }  

        public DateTime ReservedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string ReturnLocation { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
