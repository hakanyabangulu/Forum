using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ForumAPI.Models
{
    public class Role
    {
        public int RoleId { get; set; }
        [Required]
        public string RoleName { get; set; }
        public List<User> Users { get; set; } = new List<User>();
    }

    public class User
    {
        public int UserId { get; set; }
        [Required]
        public string Username { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string PasswordHash { get; set; }
        public string? AvatarUrl { get; set; }
        public string Status { get; set; } = "Active"; // New field
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int RoleId { get; set; }
        [JsonIgnore]
        public Role Role { get; set; }
        public List<Topic> Topics { get; set; } = new List<Topic>();
        public List<Comment> Comments { get; set; } = new List<Comment>();
        public List<Message> MessagesSent { get; set; } = new List<Message>();
        public List<Message> MessagesReceived { get; set; } = new List<Message>();
        public List<Notification> Notifications { get; set; } = new List<Notification>();
    }

    public class Category
    {
        public int CategoryId { get; set; }
        [Required]
        public string CategoryName { get; set; }
        public int? ParentCategoryId { get; set; }
        [JsonIgnore]
        public Category ParentCategory { get; set; }
        public List<Category> SubCategories { get; set; } = new List<Category>();
        public List<Topic> Topics { get; set; } = new List<Topic>();
    }

    public class Topic
    {
        public int TopicId { get; set; }
        public int UserId { get; set; }
        public int CategoryId { get; set; }
        [Required]
        public string Title { get; set; }
        [Required]
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        [JsonIgnore]
        public User User { get; set; }
        [JsonIgnore]
        public Category Category { get; set; }
        public List<Comment> Comments { get; set; } = new List<Comment>();
    }

    public class Comment
    {
        public int CommentId { get; set; }
        public int TopicId { get; set; }
        public int UserId { get; set; }
        [Required]
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        [JsonIgnore]
        public Topic Topic { get; set; }
        [JsonIgnore]
        public User User { get; set; }
    }

    public class Message
    {
        public int MessageId { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        [Required]
        public string Content { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;
        [JsonIgnore]
        public User Sender { get; set; }
        [JsonIgnore]
        public User Receiver { get; set; }
    }

    public class Notification
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        [Required]
        public string Message { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        [JsonIgnore]
        public User User { get; set; }
    }
}