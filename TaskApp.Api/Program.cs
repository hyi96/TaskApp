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

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow)));

var api = app.MapGroup("/api");

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
