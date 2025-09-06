using ForumAPI.Data;
using ForumAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using BCrypt.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace ForumAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ForumDbContext _context;
        private readonly IConfiguration _configuration;

        public UsersController(ForumDbContext context, IConfiguration configuration)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            try
            {
                var users = await _context.Users
                    .Include(u => u.Role)
                    .Select(u => new
                    {
                        u.UserId,
                        u.Username,
                        u.Email,
                        u.AvatarUrl,
                        u.Status,
                        RoleName = u.Role != null ? u.Role.RoleName : null,
                        u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetUser(int id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Role)
                    .Select(u => new
                    {
                        u.UserId,
                        u.Username,
                        u.Email,
                        u.AvatarUrl,
                        u.Status,
                        RoleName = u.Role != null ? u.Role.RoleName : null,
                        u.CreatedAt
                    })
                    .FirstOrDefaultAsync(u => u.UserId == id);

                if (user == null)
                {
                    return NotFound(new { message = "Kullanıcı bulunamadı" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<object>> GetCurrentUser()
        {
            try
            {
                var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserIdString) || !int.TryParse(currentUserIdString, out var userId))
                {
                    return StatusCode(401, new { message = "Geçersiz veya eksik token: Kullanıcı ID alınamadı." });
                }

                var user = await _context.Users
                    .Include(u => u.Role)
                    .Include(u => u.Topics)
                    .Include(u => u.Comments)
                    .Include(u => u.Notifications)
                    .Select(u => new
                    {
                        u.UserId,
                        u.Username,
                        u.Email,
                        u.AvatarUrl,
                        u.Status,
                        RoleName = u.Role != null ? u.Role.RoleName : null,
                        u.CreatedAt,
                        u.Topics,
                        u.Comments,
                        u.Notifications
                    })
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    return NotFound(new { message = "Kullanıcı bulunamadı" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult> PostUser([FromBody] RegisterRequest user)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                if (await _context.Users.AnyAsync(u => u.Username == user.Username || u.Email == user.Email))
                {
                    return BadRequest(new { message = "Kullanıcı adı veya e-posta zaten kullanılıyor" });
                }

                var newUser = new User
                {
                    Username = user.Username,
                    Email = user.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.Password),
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    RoleId = 3 // Default role (Member)
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                var keyString = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key tanımlanmamış");
                var key = Encoding.UTF8.GetBytes(keyString);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, newUser.UserId.ToString()),
                        new Claim(ClaimTypes.Name, newUser.Username ?? ""),
                        new Claim(ClaimTypes.Role, "Member")
                    }),
                    Expires = DateTime.UtcNow.AddDays(7),
                    Issuer = _configuration["Jwt:Issuer"],
                    Audience = _configuration["Jwt:Audience"],
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature
                    )
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                return Ok(new
                {
                    message = "Kayıt başarılı",
                    user = new
                    {
                        newUser.UserId,
                        newUser.Username,
                        newUser.Email,
                        newUser.AvatarUrl,
                        newUser.Status,
                        RoleName = "Member",
                        newUser.CreatedAt
                    },
                    token = tokenString
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutUser(int id, [FromBody] UpdateUserRequest updateUser)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(updateUser.Username) || string.IsNullOrWhiteSpace(updateUser.Email))
                {
                    return BadRequest(new { message = "Kullanıcı adı ve e-posta boş olamaz." });
                }

                if (id != updateUser.UserId)
                {
                    return BadRequest(new { message = "ID uyuşmazlığı: Gönderilen UserId, URL'deki id ile eşleşmiyor." });
                }

                var existingUser = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == id);
                if (existingUser == null)
                {
                    return NotFound(new { message = "Kullanıcı bulunamadı." });
                }

                var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserIdString) || !int.TryParse(currentUserIdString, out var currentUserId))
                {
                    return StatusCode(401, new { message = "Geçersiz veya eksik token: Kullanıcı ID alınamadı." });
                }

                if (currentUserId != id)
                {
                    return StatusCode(403, new { message = "Yetkisiz erişim: Yalnızca kendi profilinizi güncelleyebilirsiniz." });
                }

                if (await _context.Users.AnyAsync(u => u.Username == updateUser.Username && u.UserId != id))
                {
                    return BadRequest(new { message = "Bu kullanıcı adı zaten kullanılıyor." });
                }
                if (await _context.Users.AnyAsync(u => u.Email == updateUser.Email && u.UserId != id))
                {
                    return BadRequest(new { message = "Bu e-posta adresi zaten kullanılıyor." });
                }

                existingUser.Username = updateUser.Username;
                existingUser.Email = updateUser.Email;
                existingUser.AvatarUrl = updateUser.AvatarUrl ?? existingUser.AvatarUrl;
                if (!string.IsNullOrEmpty(updateUser.Password))
                {
                    existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateUser.Password);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Profil başarıyla güncellendi.",
                    user = new
                    {
                        existingUser.UserId,
                        existingUser.Username,
                        existingUser.Email,
                        existingUser.AvatarUrl,
                        existingUser.Status,
                        RoleName = existingUser.Role != null ? existingUser.Role.RoleName : null,
                        existingUser.CreatedAt
                    }
                });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new { message = "Veritabanı hatası", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _context.Users
                .Include(u => u.Topics)
                .Include(u => u.Comments)
                .Include(u => u.Notifications)
                .Include(u => u.MessagesSent)    // SenderId
                .Include(u => u.MessagesReceived) // ReceiverId
                .FirstOrDefaultAsync(u => u.UserId == id);

                if (user != null)
                {
                    _context.Topics.RemoveRange(user.Topics);
                    _context.Comments.RemoveRange(user.Comments);
                    _context.Notifications.RemoveRange(user.Notifications);
                    _context.Messages.RemoveRange(user.MessagesSent);
                    _context.Messages.RemoveRange(user.MessagesReceived);
                    _context.Users.Remove(user);

                    await _context.SaveChangesAsync();
                }

                return NoContent();
            }
            catch (DbUpdateException ex)
            {
                // Log detailed database error
                Console.WriteLine($"Database error in DeleteUser: {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { message = "Veritabanı hatası", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                // Log general error
                Console.WriteLine($"Error in DeleteUser: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpPut("ban/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BanUser(int id)
        {
            try
            {
                var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserIdString) || !int.TryParse(currentUserIdString, out var currentUserId))
                {
                    return StatusCode(401, new { message = "Geçersiz veya eksik token: Kullanıcı ID alınamadı." });
                }

                if (currentUserId == id)
                {
                    return BadRequest(new { message = "Kendinizi banlayamazsınız." });
                }

                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == id);
                if (user == null)
                {
                    return NotFound(new { message = "Kullanıcı bulunamadı." });
                }

                if (user.Status == "Banned")
                {
                    return BadRequest(new { message = "Kullanıcı zaten banlanmış." });
                }

                user.Status = "Banned";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Kullanıcı başarıyla banlandı.",
                    user = new
                    {
                        user.UserId,
                        user.Username,
                        user.Email,
                        user.AvatarUrl,
                        user.Status,
                        RoleName = user.Role != null ? user.Role.RoleName : null,
                        user.CreatedAt
                    }
                });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new { message = "Veritabanı hatası", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpPut("unban/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnbanUser(int id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == id);
                if (user == null)
                {
                    return NotFound(new { message = "Kullanıcı bulunamadı." });
                }

                if (user.Status != "Banned")
                {
                    return BadRequest(new { message = "Kullanıcı zaten banlı değil." });
                }

                user.Status = "Active";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Kullanıcı banı başarıyla kaldırıldı.",
                    user = new
                    {
                        user.UserId,
                        user.Username,
                        user.Email,
                        user.AvatarUrl,
                        user.Status,
                        RoleName = user.Role != null ? user.Role.RoleName : null,
                        user.CreatedAt
                    }
                });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new { message = "Veritabanı hatası", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            if (string.IsNullOrWhiteSpace(loginRequest.Username) || string.IsNullOrWhiteSpace(loginRequest.Password))
            {
                return BadRequest(new { message = "Kullanıcı adı ve şifre zorunludur" });
            }

            try
            {
                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Username == loginRequest.Username);

                if (user == null)
                {
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });
                }

                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    return Unauthorized(new { message = "Kullanıcı için parola tanımlı değil" });
                }

                if (!BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.PasswordHash))
                {
                    return Unauthorized(new { message = "Geçersiz şifre" });
                }

                if (user.Status == "Banned")
                {
                    return Unauthorized(new { message = "Hesabınız banlanmış. Giriş yapamazsınız." });
                }

                var keyString = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key tanımlanmamış");
                var key = Encoding.UTF8.GetBytes(keyString);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                        new Claim(ClaimTypes.Name, user.Username ?? ""),
                        new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "Member")
                    }),
                    Expires = DateTime.UtcNow.AddDays(7),
                    Issuer = _configuration["Jwt:Issuer"],
                    Audience = _configuration["Jwt:Audience"],
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature
                    )
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                return Ok(new
                {
                    message = "Giriş başarılı",
                    user = new
                    {
                        user.UserId,
                        user.Username,
                        user.Email,
                        user.AvatarUrl,
                        user.Status,
                        RoleName = user.Role != null ? user.Role.RoleName : null,
                        user.CreatedAt
                    },
                    token = tokenString
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateUserRequest
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string? Password { get; set; }
    }
}