namespace Iroh.Models.DTOs.Child
{
    public class ChildCreateDto
    {
        public required string Name { get; set; }
        public DateTime? BirthDate { get; set; }
    }

    public class ChildUpdateDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public DateTime? BirthDate { get; set; }
    }

    // Create yanıtı — isDeleted/timestamps/parent-nav sızdırmaz.
    public class ChildDto
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public required string Name { get; set; }
        public DateTime BirthDate { get; set; }

        public static ChildDto From(Iroh.Models.Entities.Child c) => new()
        {
            Id = c.Id,
            ParentId = c.ParentId,
            Name = c.Name,
            BirthDate = c.BirthDate
        };
    }

    public class UnifiedSearchResultDto
    {
        public int child_id { get; set; }
        public string child_name { get; set; } = string.Empty;
        public int parent_id { get; set; }
        public string parent_name { get; set; } = string.Empty;
        public string parent_phone { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal remaining_hours { get; set; }
        public bool is_active { get; set; }
        public string? current_table_name { get; set; }
    }
}
