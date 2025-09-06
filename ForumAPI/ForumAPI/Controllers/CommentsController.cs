using ForumAPI.Data;
using ForumAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ForumAPI.Controllers
{
    public class CommentCreateRequest
    {
        public int TopicId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ForumDbContext _context;

        public CommentsController(ForumDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet("topic/{topicId}")]
        public async Task<ActionResult<IEnumerable<Comment>>> GetComments(int topicId)
        {
            try
            {
                var comments = await _context.Comments
                    .Where(c => c.TopicId == topicId)
                    .Include(c => c.User)
                    .ToListAsync();

                return Ok(comments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<Comment>> GetComment(int id)
        {
            try
            {
                var comment = await _context.Comments
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.CommentId == id);

                if (comment == null)
                    return NotFound(new { message = "Yorum bulunamadı" });

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (comment.UserId != userId && !User.IsInRole("Admin"))
                    return Forbid("Yetkisiz erişim");

                return Ok(comment);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Comment>> PostComment([FromBody] CommentCreateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { message = "İçerik boş olamaz." });
            }

            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                var comment = new Comment
                {
                    TopicId = request.TopicId,
                    Content = request.Content,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetComment), new { id = comment.CommentId }, comment);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutComment(int id, [FromBody] Comment updatedComment)
        {
            if (updatedComment == null || id != updatedComment.CommentId)
            {
                return BadRequest(new { message = "ID uyuşmazlığı veya eksik veri." });
            }

            try
            {
                var comment = await _context.Comments.FindAsync(id);
                if (comment == null)
                    return NotFound(new { message = "Yorum bulunamadı" });

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (comment.UserId != userId && !User.IsInRole("Admin"))
                    return Forbid("Yetkisiz erişim");

                comment.Content = updatedComment.Content;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(int id)
        {
            try
            {
                var comment = await _context.Comments.FindAsync(id);
                if (comment == null)
                    return NotFound(new { message = "Yorum bulunamadı" });

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (comment.UserId != userId && !User.IsInRole("Admin"))
                    return Forbid("Yetkisiz erişim");

                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }
    }
}