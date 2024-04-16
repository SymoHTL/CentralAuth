using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AuthApi.Extensions;

public static class BuilderExtensions {
    public static IHostApplicationBuilder AddMetrics(this IHostApplicationBuilder builder) {
        builder.Logging.AddOpenTelemetry(logging => {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });
        
        
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();

                metrics.AddMeter(
                    "Microsoft.AspNetCore.Hosting",
                    "Microsoft.AspNetCore.Server.Kestrel",
                    "System.Net.Http");
                metrics.AddView("request-duration",
                    new ExplicitBucketHistogramConfiguration {
                        Boundaries = [
                            0, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 10, 25, 50, 100, 250, 500,
                            1000
                        ]
                    });
            })
            .WithTracing(tracing => {
                if (builder.Environment.IsDevelopment())
                    tracing.SetSampler(new AlwaysOnSampler());

                tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
            });
        
        // health checks
        builder.Services.AddHealthChecks()
            .AddCheck("self", () =>
                HealthCheckResult.Healthy(), ["live"]);
        
        builder.Services.AddServiceDiscovery();
        
        builder.Services.ConfigureHttpClientDefaults(http => {
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static WebApplication MapMetrics(this WebApplication app) {
        app.UseForwardedHeaders();

        app.UseRouting();
        
        //app.UseWhen(context => context.Request.Path.StartsWithSegments("/metrics"),
        //    appBuilder => { appBuilder.UseMiddleware<PrometheusOnlyMiddleware>(); });
        
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        app.MapPrometheusScrapingEndpoint();
        
        app.MapHealthChecks("/health");

        app.MapHealthChecks("/alive", new HealthCheckOptions {
            Predicate = r => r.Tags.Contains("live")
        });
        return app;
    }
}