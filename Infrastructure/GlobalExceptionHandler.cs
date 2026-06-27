using Iroh.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Iroh.Infrastructure
{
    // Tüm yakalanmayan exception'ları tek noktada RFC7807 ProblemDetails'e çevirir.
    // İç hata detayını (ex.Message) yalnızca bilinen iş/404 hatalarında dışarı verir; gerisi generic 500 + log.
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetailsService;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
        {
            _problemDetailsService = problemDetailsService;
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (status, title) = Map(exception);

            if (status == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Yakalanmayan hata");
            }
            else if (exception is DbUpdateException)
            {
                // Veritabanı kısıt ihlali → kullanıcı hatası (400); görünür olsun diye uyarı.
                _logger.LogWarning(exception, "Veritabanı kısıt ihlali → 400: {Title}", title);
            }

            httpContext.Response.StatusCode = status;
            return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails { Status = status, Title = title }
            });
        }

        // İstisnayı (status, kullanıcıya gösterilecek başlık) çiftine eşler.
        private static (int status, string title) Map(Exception exception) => exception switch
        {
            BusinessRuleException => (StatusCodes.Status400BadRequest, exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            // EF Core'un sardığı Postgres kısıt ihlallerini anlamlı 400'lere çevir (generic 500 yerine).
            DbUpdateException { InnerException: PostgresException pg } => MapPostgres(pg),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu.")
        };

        // Postgres SqlState → anlaşılır mesaj. Bilinmeyen DB hataları 500 kalır.
        private static (int status, string title) MapPostgres(PostgresException pg) => pg.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => (StatusCodes.Status400BadRequest, "Bu kayıt zaten mevcut."),
            PostgresErrorCodes.ForeignKeyViolation => (StatusCodes.Status400BadRequest, "Bu kayıt başka kayıtlara bağlı olduğundan işlem tamamlanamadı."),
            PostgresErrorCodes.NotNullViolation => (StatusCodes.Status400BadRequest, "Zorunlu bir alan boş bırakılamaz."),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir veritabanı hatası oluştu.")
        };
    }
}
