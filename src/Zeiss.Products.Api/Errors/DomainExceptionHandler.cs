using Microsoft.AspNetCore.Diagnostics;
using Zeiss.Products.Api.Responses;
using Zeiss.Products.Domain.Exceptions;

namespace Zeiss.Products.Api.Errors;

// Business rule violations (duplicate name, insufficient stock, concurrent change, exhausted IDs)
// conflict with the current state, so they return 409 with their message.
// Any other exception falls through to the generic 500 response configured in Program.
public class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(
            new ErrorResponse { Message = exception.Message }, cancellationToken);
        return true;
    }
}
