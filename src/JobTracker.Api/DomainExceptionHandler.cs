using JobTracker.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace JobTracker.Api;

/// <summary>Нарушение бизнес-правила — ошибка клиента: отвечаем 400 с ProblemDetails, а не 500.</summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not DomainException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Title = "Некорректные данные", Detail = exception.Message, Status = 400 },
        });
    }
}
