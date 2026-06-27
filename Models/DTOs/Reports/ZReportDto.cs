namespace Iroh.Models.DTOs.Reports
{
    // Gün-sonu nakit özeti — cash_ledger agregasyonu (şema değişmeden). Tutarlar amount_delta
    // doğal işaretiyle (Charge −, Payment +, Refund −, Adjustment ±).
    public class ZReportDto
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }

        public int TransactionCount { get; set; }

        public decimal TotalPayment { get; set; }      // Σ Payment (+) — tahsil edilen nakit
        public decimal TotalCharge { get; set; }       // Σ Charge (−) — kaydedilen ücret/borç
        public decimal TotalRefund { get; set; }       // Σ Refund (−) — iade
        public decimal TotalAdjustment { get; set; }   // Σ Adjustment (±) — düzeltme
        public decimal NetCash { get; set; }           // Payment + Refund + Adjustment (kasaya net giren)

        public List<ZReportTypeLineDto> ByType { get; set; } = new();
        public List<ZReportUserLineDto> ByUser { get; set; } = new();
    }

    public class ZReportTypeLineDto
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Total { get; set; }
    }

    public class ZReportUserLineDto
    {
        public int? UserId { get; set; }
        public int Count { get; set; }
        public decimal Total { get; set; }
    }
}
