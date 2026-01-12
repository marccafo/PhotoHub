using System.ComponentModel.DataAnnotations;

namespace PhotoHub.API.Shared.Models;

public class Setting
{
    [Key]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string Value { get; set; } = string.Empty;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
