using System.ComponentModel.DataAnnotations.Schema;

namespace Library.Models
{
    [Table("books_availability")] 
    public class BookCopy
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public Book Book { get; set; }
        public string? Location { get; set; }  
        public string Status { get; set; } = "Available";
    }
}
