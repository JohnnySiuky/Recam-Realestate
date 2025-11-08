using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Recam.Common.Filters;

public class ApiValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState.Where(x => x.Value!.Errors.Count > 0).ToDictionary(k => k.Key, v => v.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
            context.Result = new BadRequestObjectResult(new
            {
                succeed = false,
                error = new { code = "ValidationErrors", message = "Validation Failed", details = errors }
            });
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) 
    {
    }
}