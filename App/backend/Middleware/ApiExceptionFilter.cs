using DatSan.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DatSan.Api.Middleware;

// Expected business errors (such as wrong credentials) are responses, not server failures.
public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApiException error) return;
        context.Result = new ObjectResult(new ApiError(error.Message, error.Code))
        {
            StatusCode = error.StatusCode
        };
        context.ExceptionHandled = true;
    }
}
