using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Library.Models;
using Microsoft.AspNetCore.Authorization;

namespace Library.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly LibraryContext _context;

        public BooksController(LibraryContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IEnumerable<Book>> Get()
        {
            return await _context.Books.ToListAsync();
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Book book)
        {
            if (string.IsNullOrWhiteSpace(book.Title) || string.IsNullOrWhiteSpace(book.Author))
            {
                return BadRequest("Title and Author are required.");
            }

            _context.Books.Add(book);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = book.Id }, book);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("search")]
        public async Task<IEnumerable<object>> Search(
            [FromQuery] string? author,
            [FromQuery] string? location,
            [FromQuery] bool? available)
        {
            var query = _context.BookCopies
                .Include(bc => bc.Book)
                .AsQueryable();

            if (!string.IsNullOrEmpty(author))
                query = query.Where(bc => bc.Book.Author == author);

            if (!string.IsNullOrEmpty(location))
                query = query.Where(bc => bc.Location == location);

            if (available.HasValue)
            {
                if (available.Value)
                    query = query.Where(bc => bc.Status == "Available");
                else
                    query = query.Where(bc => bc.Status != "Available");
            }

            return await query
                .Select(bc => new
                {
                    BookCopyId = bc.Id,
                    bc.Location,
                    bc.Status,
                    bc.Book.Title,
                    bc.Book.Author
                })
                .ToListAsync();
        }

        [HttpPost("{bookCopyId}/reserve")]
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Reservation(int bookCopyId, [FromBody] ReserveDto request)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized("Invalid user ID.");
            }

            var activeCount = await _context.Reservations
                .CountAsync(r => r.UserId == userId && r.IsActive);

            if (activeCount >= 3)
            {
                return BadRequest("You already have 3 active books. Cannot reserve more.");
            }

            var bookCopy = await _context.BookCopies
                .Include(bc => bc.Book)
                .FirstOrDefaultAsync(bc => bc.Id == bookCopyId && bc.Status == "Available");

            if (bookCopy == null)
            {
                return BadRequest("Book copy not available.");
            }

            var reservation = new Reservation
            {
                BookCopyId = bookCopy.Id,
                UserId = userId,
                ReservedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                ReturnLocation = request.ReturnLocation
            };

            _context.Reservations.Add(reservation);
            bookCopy.Status = "Reserved";
            await _context.SaveChangesAsync();

            return Ok("Book reserved successfully.");
        }

        [HttpGet("my-reservations")]
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> GetMyReservations()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized("Invalid user ID.");
            }

            var reservations = await _context.Reservations
                .Include(r => r.BookCopy)
                .ThenInclude(bc => bc.Book)
                .Where(r => r.UserId == userId && r.IsActive)
                .Select(r => new
                {
                    r.Id,
                    r.BookCopyId,
                    BookTitle = r.BookCopy.Book.Title,
                    BookAuthor = r.BookCopy.Book.Author,
                    r.ReservedAt,
                    r.ExpiresAt,
                    r.ReturnLocation,
                    r.IsActive
                })
                .ToListAsync();

            return Ok(reservations);
        }
    }
}
