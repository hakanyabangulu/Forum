using ForumAPI.Data;
using ForumAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ForumAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TopicsController : ControllerBase
    {
        private readonly ForumDbContext _context;

        public TopicsController(ForumDbContext context)
        {
            _context = context;
        }

        public class TopicCreateDto
        {
            public string Title { get; set; }
            public string Content { get; set; }
            public int CategoryId { get; set; }
        }

        // GET: api/topics
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Topic>>> GetTopics()
        {
            var topics = await _context.Topics
                .Include(t => t.Comments)
                .Include(t => t.User)
                .Include(t => t.Category)
                .OrderByDescending(t => t.CreatedAt) // En yeni en üstte
                .ToListAsync();

            return Ok(topics);
        }

        // GET: api/topics/my-topics
        [HttpGet("my-topics")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Topic>>> GetMyTopics()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Geçersiz kullanıcı" });

            var topics = await _context.Topics
                .Where(t => t.UserId == userId)
                .Include(t => t.Comments)
                .Include(t => t.User)
                .Include(t => t.Category)
                .OrderByDescending(t => t.CreatedAt) // En yeni en üstte
                .ToListAsync();

            return Ok(topics);
        }

        // GET: api/topics/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Topic>> GetTopic(int id)
        {
            var topic = await _context.Topics
                .Include(t => t.Comments)
                .Include(t => t.User)
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.TopicId == id);

            if (topic == null)
                return NotFound(new { message = "Konu bulunamadı" });

            return Ok(topic);
        }

        // POST: api/topics
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Topic>> PostTopic([FromBody] TopicCreateDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized(new { message = "Geçersiz kullanıcı" });

            if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Content))
                return BadRequest(new { message = "Başlık ve içerik boş olamaz" });

            if (!await _context.Categories.AnyAsync(c => c.CategoryId == dto.CategoryId))
                return BadRequest(new { message = "Geçersiz kategori ID" });

            var topic = new Topic
            {
                Title = dto.Title,
                Content = dto.Content,
                CategoryId = dto.CategoryId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                User = null,
                Category = null,
                Comments = new List<Comment>()
            };

            _context.Topics.Add(topic);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTopic), new { id = topic.TopicId }, topic);
        }

        // PUT: api/topics/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutTopic(int id, [FromBody] TopicCreateDto dto)
        {
            var existingTopic = await _context.Topics.FindAsync(id);
            if (existingTopic == null)
                return NotFound(new { message = "Konu bulunamadı" });

            var userId = GetCurrentUserId();
            if (existingTopic.UserId != userId && !User.IsInRole("Admin"))
                return Forbid("Yetkisiz erişim");

            if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Content))
                return BadRequest(new { message = "Başlık ve içerik boş olamaz" });

            if (!await _context.Categories.AnyAsync(c => c.CategoryId == dto.CategoryId))
                return BadRequest(new { message = "Geçersiz kategori ID" });

            existingTopic.Title = dto.Title;
            existingTopic.Content = dto.Content;
            existingTopic.CategoryId = dto.CategoryId;
            existingTopic.User = null;
            existingTopic.Category = null;

            await _context.SaveChangesAsync();
            return NoContent();
        }
        [HttpGet("byCategory/{categoryId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetTopicsByCategory(int categoryId)
        {
            try
            {
                var topics = await _context.Topics
                    .Where(t => t.CategoryId == categoryId)
                    .Include(t => t.Comments)
                    .Include(t => t.User)
                        .ThenInclude(u => u.Role) // Role bilgisi için
                    .Include(t => t.Category)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new
                    {
                        t.TopicId,
                        t.Title,
                        t.Content,
                        t.CreatedAt,
                        t.UserId,
                        User = t.User == null ? null : new
                        {
                            t.User.UserId,
                            t.User.Username,
                            RoleName = t.User.Role != null ? t.User.Role.RoleName : null
                        },
                        t.CategoryId,
                        Category = t.Category == null ? null : new
                        {
                            t.Category.CategoryId,
                            t.Category.CategoryName
                        },
                        Comments = t.Comments
                    })
                    .ToListAsync();

                return Ok(topics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Konular yüklenemedi", detail = ex.Message });
            }
        }

        // DELETE: api/topics/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteTopic(int id)
        {
            try
            {
                var topic = await _context.Topics.FindAsync(id);
                if (topic == null)
                    return NotFound(new { message = "Konu bulunamadı" });

                var userId = GetCurrentUserId();
                if (topic.UserId != userId && !User.IsInRole("Admin"))
                    return Forbid("Yetkisiz erişim");

                _context.Topics.Remove(topic);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (DbUpdateException ex)
            {
                // Yabancı anahtar (foreign key) kısıtlaması gibi veritabanı hatalarını yakala
                // Bu hata, konunun ilişkili olduğu yorumlar gibi verilerin silinmesi gerektiğini ancak silinemediğini gösterebilir.
                // ex'i loglayarak daha fazla bilgi edinebilirsiniz.
                // Log.Error(ex, "Konu silinirken veritabanı hatası oluştu.");
                return StatusCode(500, new { message = "Konu silinirken bir veritabanı hatası oluştu. İlişkili verileri kontrol edin." });
            }
            catch (Exception ex)
            {
                // Diğer genel hataları yakala
                // Log.Error(ex, "Konu silinirken beklenmeyen bir hata oluştu.");
                return StatusCode(500, new { message = "Beklenmeyen bir hata oluştu." });
            }
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int userId) ? userId : 0;
        }
    }
}