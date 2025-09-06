using ForumAPI.Data;
using ForumAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;

namespace ForumAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly ForumDbContext _context;

        public MessagesController(ForumDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // GET: api/Messages
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<object>>> GetMessages()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var messages = await _context.Messages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => new
                    {
                        m.MessageId,
                        m.SenderId,
                        m.ReceiverId,
                        SenderUserName = m.Sender.Username,
                        ReceiverUserName = m.Receiver.Username,
                        m.Content,
                        m.SentAt,
                        CurrentUserId = userId
                    })
                    .ToListAsync();

                return Ok(messages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        // GET: api/Messages/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<object>> GetMessage(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var message = await _context.Messages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .Where(m => m.MessageId == id)
                    .Select(m => new
                    {
                        m.MessageId,
                        m.SenderId,
                        m.ReceiverId,
                        SenderUserName = m.Sender.Username,
                        ReceiverUserName = m.Receiver.Username,
                        m.Content,
                        m.SentAt,
                        CurrentUserId = userId
                    })
                    .FirstOrDefaultAsync();

                if (message == null)
                {
                    return NotFound(new { message = "Mesaj bulunamadı" });
                }

                if (message.SenderId != userId && message.ReceiverId != userId)
                {
                    return Forbid("Yetkisiz erişim");
                }

                return Ok(message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        // POST: api/Messages
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Message>> PostMessage([FromBody] MessageRequest messageRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var receiverExists = await _context.Users.AnyAsync(u => u.UserId == messageRequest.ReceiverId);
                if (!receiverExists)
                {
                    return BadRequest(new { message = "Alıcı kullanıcı bulunamadı" });
                }

                var message = new Message
                {
                    SenderId = userId,
                    ReceiverId = messageRequest.ReceiverId,
                    Content = messageRequest.Content,
                    SentAt = DateTime.UtcNow
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetMessage), new { id = message.MessageId }, message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        // PUT: api/Messages/{id}
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutMessage(int id, [FromBody] MessageRequest updatedMessage)
        {
            try
            {
                var message = await _context.Messages.FindAsync(id);
                if (message == null)
                {
                    return NotFound(new { message = "Mesaj bulunamadı" });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (message.SenderId != userId)
                {
                    return Forbid("Yetkisiz erişim");
                }

                message.Content = updatedMessage.Content;
                message.SentAt = DateTime.UtcNow; // Güncelleme zamanı
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        // DELETE: api/Messages/{id}
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            try
            {
                var message = await _context.Messages.FindAsync(id);
                if (message == null)
                {
                    return NotFound(new { message = "Mesaj bulunamadı" });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (message.SenderId != userId)
                {
                    return Forbid("Yetkisiz erişim");
                }

                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        // GET: api/Messages/conversation/{receiverId}
        [HttpGet("conversation/{receiverId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<object>>> GetConversationWith(int receiverId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var conversation = await _context.Messages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .Where(m =>
                        (m.SenderId == userId && m.ReceiverId == receiverId) ||
                        (m.SenderId == receiverId && m.ReceiverId == userId))
                    .OrderBy(m => m.SentAt)
                    .Select(m => new
                    {
                        id = m.MessageId,
                        senderId = m.SenderId,
                        receiverId = m.ReceiverId,
                        content = m.Content,
                        sentAt = m.SentAt
                    })
                    .ToListAsync();

                return Ok(conversation);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }
    }

    public class MessageRequest
    {
        public int ReceiverId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}