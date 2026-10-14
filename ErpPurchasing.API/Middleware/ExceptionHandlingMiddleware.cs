using ErpPurchasing.API.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ErpPurchasing.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);   // İsteği bir sonraki adıma (controller'a) ilet
        }
        catch (AppException ex)
        {
            // Beklenen hata: Kullanıcıya açıklayıcı mesaj gösterilir
            _logger.LogWarning("{Title}: {Message}", ex.Title, ex.Message);
            await WriteProblemAsync(context, ex.StatusCode, ex.Title, ex.Message);
        }
        catch (Exception ex)
        {
            // Beklenmeyen hata: Detay SADECE loga yazılır, kullanıcıya genel mesaj gösterilir
            _logger.LogError(ex, "Beklenmeyen hata oluştu");
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "Sunucu hatası", "Beklenmeyen bir hata oluştu. Lütfen sistem yöneticisine başvurun.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(problem);
    }
}
