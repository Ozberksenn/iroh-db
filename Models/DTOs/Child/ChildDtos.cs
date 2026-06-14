namespace Iroh.Models.DTOs.Child
{
    public class ChildCreateDto
    {
        public required string name { get; set; }
        public DateTime? birthDate { get; set; }
    }

    public class ChildUpdateDto
    {
        public int id { get; set; }
        public required string name { get; set; }
        public DateTime? birthDate { get; set; }
    }

    // Create yanıtı — isDeleted/timestamps/parent-nav sızdırmaz.
    public class ChildDto
    {
        public int id { get; set; }
        public int parentId { get; set; }
        public required string name { get; set; }
        public DateTime birthDate { get; set; }

        public static ChildDto From(Iroh.Models.Entities.Child c) => new()
        {
            id = c.id,
            parentId = c.parentId,
            name = c.name,
            birthDate = c.birthDate
        };
    }

    public class UnifiedSearchResultDto
    {
        public int child_id { get; set; }
        public string child_name { get; set; } = string.Empty;
        public int parent_id { get; set; }
        public string parent_name { get; set; } = string.Empty;
        public string parent_phone { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public decimal remaining_hours { get; set; }
        public bool is_active { get; set; }
        public string? current_table_name { get; set; }
    }
}
