using System.ComponentModel.DataAnnotations.Schema;

namespace Library.Models
{
    [Table("libraries")]  
    public class LibraryBranch
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
    }
}

