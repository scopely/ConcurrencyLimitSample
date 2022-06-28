using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConcurrencyLimitSample
{
    public static class ConcurrencyLimitMiddleware
    {
        public static IApplicationBuilder UseConcurrencyLimit(this IApplicationBuilder app, int maxConcurrency, TimeSpan waitTimeout)
        {
            var sem = new SemaphoreSlim(maxConcurrency);
            var reportTimer = new Timer(o =>
            {
                // report the number of requests in flight once per second
                MetricReporter.Gauge("requests.concurrent", sem.CurrentCount);
            }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            return app.Use(async (context, next) =>
            {
                if (!await sem.WaitAsync(waitTimeout))
                {
                    // reject requests outright if it takes too long to get a concurrency slot
                    // also, note that the client can check for this status code and
                    // retry the request with some exponential backoff
                    context.Response.StatusCode = 503;
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync("Service Unavailable");
                    return;
                }
                try
                {
                    await next();
                }
                finally
                {
                    sem.Release();
                }
            });
        }
    }
}
