using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

Log.Logger = new LoggerConfiguration()
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = "http://localhost:4317";
        // Prevent tracing of outbound requests from the sink
        options.HttpMessageHandler = new SocketsHttpHandler { ActivityHeadersPropagator = null };
        options.IncludedData =
            IncludedData.SpanIdField
            | IncludedData.TraceIdField
            | IncludedData.MessageTemplateTextAttribute;

        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = builder.Environment.ApplicationName,
            ["environment"] = builder.Environment.EnvironmentName
        };
        options.BatchingOptions.BatchSizeLimit = 700;
        options.BatchingOptions.BufferingTimeLimit = TimeSpan.FromSeconds(1);
        options.BatchingOptions.QueueLimit = 10;
    })
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog(Log.Logger);

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
    options.ParseStateValues = true;
});

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
    .WithTracing(tracerBuilder =>
    {
        tracerBuilder
            .AddAspNetCoreInstrumentation();
    })
    .WithMetrics(metricsBuilder =>
    {
        metricsBuilder
            .AddAspNetCoreInstrumentation()
            .AddProcessInstrumentation()
            .AddRuntimeInstrumentation();
    })
    .UseOtlpExporter();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseSerilogRequestLogging();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", (HttpContext httpContext) =>
    {
        app.Logger.LogInformation("GetWeatherForecast called {At}", DateTime.Now.ToString());

        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}