namespace DataViz.Api.Models;

public class DashboardView
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Page { get; set; } = string.Empty;
    public string FiltersJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
