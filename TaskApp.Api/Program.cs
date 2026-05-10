using TaskApp.Api.Services;
using TaskApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var environment = sp.GetRequiredService<IHostEnvironment>();
    var connectionString = configuration.GetConnectionString("TaskAppCloud");

    return string.IsNullOrWhiteSpace(connectionString)
        ? TaskAppCloudDatabase.FromFile(Path.Combine(environment.ContentRootPath, "App_Data", "taskapp-cloud.db"))
        : TaskAppCloudDatabase.FromConnectionString(connectionString);
});

var app = builder.Build();

await app.Services.GetRequiredService<TaskAppCloudDatabase>().InitializeAsync();

var apiKey = app.Configuration["TaskApp:ApiKey"]
    ?? Environment.GetEnvironmentVariable("TASKAPP_API_KEY");
if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("TaskApp:ApiKey or TASKAPP_API_KEY must be configured outside Development.");
}

app.MapGet("/health", async (TaskAppCloudDatabase database) =>
{
    var status = await database.CanConnectAsync() ? "ok" : "database-unavailable";
    return status == "ok"
        ? Results.Ok(new HealthResponse(status, DateTimeOffset.UtcNow))
        : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: status);
});

var api = app.MapGroup("/api");
api.AddEndpointFilter(async (context, next) =>
{
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return await next(context);
    }

    var request = context.HttpContext.Request;
    if (!request.Headers.TryGetValue(TaskAppCloudHeaders.ApiKey, out var providedApiKey) ||
        !string.Equals(providedApiKey.ToString(), apiKey, StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    return await next(context);
});

api.MapPost("/accounts", async (CreateAccountRequest request, TaskAppCloudDatabase database) =>
{
    var account = await database.CreateAccountAsync(request.DisplayName);
    return Results.Created($"/api/accounts/{account.Id}", account);
});

api.MapGet("/accounts/{accountId:guid}/profiles", async (Guid accountId, TaskAppCloudDatabase database) =>
{
    if (!await database.AccountExistsAsync(accountId))
    {
        return Results.NotFound();
    }

    var profiles = await database.ListProfilesAsync(accountId);
    return Results.Ok(profiles);
});

api.MapPut("/accounts/{accountId:guid}/profiles/{profileId:guid}/snapshot",
    async (Guid accountId, Guid profileId, UpsertProfileSnapshotRequest request, TaskAppCloudDatabase database) =>
    {
        var snapshot = await database.UpsertProfileSnapshotAsync(accountId, profileId, request);
        return snapshot == null ? Results.NotFound() : Results.Ok(snapshot);
    });

api.MapGet("/accounts/{accountId:guid}/profiles/{profileId:guid}/snapshot",
    async (Guid accountId, Guid profileId, TaskAppCloudDatabase database) =>
    {
        var snapshot = await database.GetProfileSnapshotAsync(accountId, profileId);
        return snapshot == null ? Results.NotFound() : Results.Ok(snapshot);
    });

app.Run();

public partial class Program;
