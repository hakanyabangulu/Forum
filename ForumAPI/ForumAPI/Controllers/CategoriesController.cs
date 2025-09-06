using ForumAPI.Data;
using ForumAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ForumApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ForumDbContext _context;

        public CategoriesController(ForumDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Include(c => c.ParentCategory)
                    .Include(c => c.SubCategories)
                    .ToListAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching categories: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Category>> GetCategory(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.ParentCategory)
                    .Include(c => c.SubCategories)
                    .FirstOrDefaultAsync(c => c.CategoryId == id);

                if (category == null)
                {
                    return NotFound(new { message = "Kategori bulunamadı" });
                }

                return Ok(category);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching category: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Category>> PostCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.CategoryName))
                {
                    return BadRequest(new { message = "Kategori adı boş olamaz." });
                }

                if (await _context.Categories.AnyAsync(c => c.CategoryName == request.CategoryName))
                {
                    return BadRequest(new { message = "Kategori adı zaten kullanılıyor." });
                }

                var category = new Category
                {
                    CategoryName = request.CategoryName,
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCategory), new { id = category.CategoryId }, new { category.CategoryId, category.CategoryName });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating category: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PutCategory(int id, [FromBody] UpdateCategoryRequest request)
        {
            try
            {
                if (id != request.CategoryId)
                {
                    return BadRequest(new { message = "ID uyuşmazlığı" });
                }

                if (string.IsNullOrWhiteSpace(request.CategoryName))
                {
                    return BadRequest(new { message = "Kategori adı boş olamaz." });
                }

                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return NotFound(new { message = "Kategori bulunamadı" });
                }

                category.CategoryName = request.CategoryName;
                category.ParentCategoryId = request.ParentCategoryId;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating category: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return NotFound(new { message = "Kategori bulunamadı" });
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting category: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Sunucu hatası", detail = ex.Message });
            }
        }
    }

    public class CreateCategoryRequest
    {
        [Required(ErrorMessage = "Kategori adı zorunludur.")]
        public string CategoryName { get; set; }
    }

    public class UpdateCategoryRequest
    {
        [Required(ErrorMessage = "Kategori ID zorunludur.")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Kategori adı zorunludur.")]
        public string CategoryName { get; set; }

        public int? ParentCategoryId { get; set; }
    }
}