namespace Iroh.Models.Responses
{
    // Başarı zarfı. Hatalar bu zarfta değil, ProblemDetails (RFC7807) olarak döner.
    public class ApiResponse<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public string? Message { get; init; }

        public static ApiResponse<T> Ok(T? data, string? message = null)
            => new() { Success = true, Data = data, Message = message };
    }

    // Tip çıkarımıyla: ApiResponse.Ok(result)
    public static class ApiResponse
    {
        public static ApiResponse<T> Ok<T>(T? data, string? message = null)
            => ApiResponse<T>.Ok(data, message);
    }
}
