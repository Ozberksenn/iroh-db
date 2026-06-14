namespace Iroh.Models.DTOs.Common
{
    public class PagedResult<T>
    {
        public List<T> items { get; set; } = new();
        public int page { get; set; }
        public int size { get; set; }
        public int totalPages { get; set; }
        public int totalSize { get; set; }
    }
}
