
using ForumAPI.Data;
using ForumAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ForumApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly ForumDbContext _context;

        public NotificationsController(ForumDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Notification>>> GetNotifications()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .ToListAsync();
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<Notification>> GetNotification(int id)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null)
                {
                    return NotFound(new { message = "Bildirim bulunamadı" });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (notification.UserId != userId)
                {
                    return Forbid("Yetkisiz erişim");
                }

                return Ok(notification);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Notification>> PostNotification(Notification notification)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                notification.UserId = userId;
                notification.CreatedAt = DateTime.Now;

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetNotification), new { id = notification.NotificationId }, notification);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutNotification(int id, Notification updatedNotification)
        {
            try
            {
                if (id != updatedNotification.NotificationId)
                {
                    return BadRequest(new { message = "ID uyuşmazlığı" });
                }

                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null)
                {
                    return NotFound(new { message = "Bildirim bulunamadı" });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (notification.UserId != userId)
                {
                    return Forbid("Yetkisiz erişim");
                }

                notification.Message = updatedNotification.Message;
                notification.IsRead = updatedNotification.IsRead;
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
        public async Task<IActionResult> DeleteNotification(int id)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null)
                {
                    return NotFound(new { message = "Bildirim bulunamadı" });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (notification.UserId != userId)
                {
                    return Forbid("Yetkisiz erişim");
                }

                _context.Notifications.Remove(notification);
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