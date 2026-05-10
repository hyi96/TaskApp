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

api.MapPost("/accounts", async (CreateAccountRequest request, TaskAppCloudDatabase database, HttpContext httpContext) =>
{
    if (!HasServerApiKey(httpContext.Request, apiKey))
    {
        return Results.Unauthorized();
    }

    var account = await database.CreateAccountAsync(request.DisplayName);
    return Results.Created($"/api/accounts/{account.Id}", account);
});

api.MapPost("/accounts/login", async (LoginAccountRequest request, TaskAppCloudDatabase database) =>
{
    var account = await database.LoginAccountAsync(request.AccountId, request.LoginSecret);
    return account == null ? Results.Unauthorized() : Results.Ok(account);
});

api.MapGet("/accounts/{accountId:guid}/profiles", async (Guid accountId, TaskAppCloudDatabase database, HttpContext httpContext) =>
{
    if (!await IsAuthorizedForAccountAsync(httpContext.Request, database, accountId, apiKey))
    {
        return Results.Unauthorized();
    }

    if (!await database.AccountExistsAsync(accountId))
    {
        return Results.NotFound();
    }

    var profiles = await database.ListProfilesAsync(accountId);
    return Results.Ok(profiles);
});

api.MapPut("/accounts/{accountId:guid}/profiles/{profileId:guid}/snapshot",
    async (Guid accountId, Guid profileId, UpsertProfileSnapshotRequest request, TaskAppCloudDatabase database, HttpContext httpContext) =>
    {
        if (!await IsAuthorizedForAccountAsync(httpContext.Request, database, accountId, apiKey))
        {
            return Results.Unauthorized();
        }

        var snapshot = await database.UpsertProfileSnapshotAsync(accountId, profileId, request);
        return snapshot == null ? Results.NotFound() : Results.Ok(snapshot);
    });

api.MapGet("/accounts/{accountId:guid}/profiles/{profileId:guid}/snapshot",
    async (Guid accountId, Guid profileId, TaskAppCloudDatabase database, HttpContext httpContext) =>
    {
        if (!await IsAuthorizedForAccountAsync(httpContext.Request, database, accountId, apiKey))
        {
            return Results.Unauthorized();
        }

        var snapshot = await database.GetProfileSnapshotAsync(accountId, profileId);
        return snapshot == null ? Results.NotFound() : Results.Ok(snapshot);
    });

app.Run();

static bool HasServerApiKey(HttpRequest request, string? apiKey)
{
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return true;
    }

    return request.Headers.TryGetValue(TaskAppCloudHeaders.ApiKey, out var providedApiKey) &&
        string.Equals(providedApiKey.ToString(), apiKey, StringComparison.Ordinal);
}

static async Task<bool> IsAuthorizedForAccountAsync(
    HttpRequest request,
    TaskAppCloudDatabase database,
    Guid accountId,
    string? apiKey)
{
    if (HasServerApiKey(request, apiKey))
    {
        return true;
    }

    return request.Headers.TryGetValue(TaskAppCloudHeaders.AccountSecret, out var providedSecret) &&
        await database.IsAccountSecretValidAsync(accountId, providedSecret.ToString());
}

public partial class Program;
