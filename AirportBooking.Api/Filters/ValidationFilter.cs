using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AirportBooking.Api.Filters;

/// <summary>
/// Runs the registered FluentValidation validator for every action argument
/// before the action body executes.
///
/// FluentValidation 12 removed the auto-validation pipeline that used to do
/// this, and the replacement guidance is to inject IValidator&lt;T&gt; into each
/// action. One filter keeps that out of every controller and, more importantly,
/// means a new endpoint cannot forget to validate.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationFilter(IServiceProvider services)
    {
        _services = services;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (result.IsValid)
            {
                continue;
            }

            var problems = new Dictionary<string, string[]>();
            foreach (var group in result.Errors.GroupBy(e => e.PropertyName))
            {
                problems[group.Key] = group.Select(e => e.ErrorMessage).ToArray();
            }

            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(problems)
            {
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest
            });

            return;
        }

        await next();
    }
}
