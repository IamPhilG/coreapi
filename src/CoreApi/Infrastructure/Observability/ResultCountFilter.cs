using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CoreApi.Infrastructure.Observability;

/// <summary>
/// Records the size of a collection response into <see cref="HttpContext.Items"/> so the request
/// log can report "results=N" for list endpoints. Only the count is captured -- never the values.
/// </summary>
public sealed class ResultCountFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: { } value }
            && value is not string
            && value is IEnumerable enumerable)
        {
            int count = value is ICollection collection
                ? collection.Count
                : enumerable.Cast<object?>().Count();
            context.HttpContext.Items[RequestObservabilityMiddleware.ResultCountItemKey] = count;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        // Nothing to do after the result executes.
    }
}
