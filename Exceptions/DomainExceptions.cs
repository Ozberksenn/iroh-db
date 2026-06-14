namespace Iroh.Exceptions
{
    // İş kuralı ihlali → HTTP 400. (Eski koddaki "throw new Exception(...)" iş-kuralı mesajlarının yerini alır.)
    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string message) : base(message) { }
    }

    // Kayıt bulunamadı → HTTP 404.
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }
}
