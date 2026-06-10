namespace Iroh.Models.DTOs.Child
{
    public class ChildCreateDto
    {
        public required string name { get; set; }
        public DateTime? birthDate { get; set; }
    }

    public class ChildUpdateDto
    {
        public required string name { get; set; }
        public DateTime? birthDate { get; set; }
    }

    public class UnifiedSearchResultDto
    {
        public long child_id { get; set; }
        public string child_name { get; set; } = string.Empty;
        public long parent_id { get; set; }
        public string parent_name { get; set; } = string.Empty;
        public string parent_phone { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public decimal remaining_hours { get; set; }
        public bool is_active { get; set; }
        public string? current_table_name { get; set; }
    }
}
