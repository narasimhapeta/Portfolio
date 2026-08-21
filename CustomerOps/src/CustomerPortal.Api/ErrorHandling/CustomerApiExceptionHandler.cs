using CustomerPortal.Application.Customers;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace CustomerPortal.Api.ErrorHandling;

public class CustomerApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ValidationException validationException:
                var errors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                await Results.ValidationProblem(errors).ExecuteAsync(httpContext);
                return true;

            case CustomerNotFoundException notFoundException:
                await Results.Problem(
                    title: "Customer not found",
                    detail: notFoundException.Message,
                    statusCode: StatusCodes.Status404NotFound
                ).ExecuteAsync(httpContext);
                return true;

            default:
                return false;
        }
    }
}
