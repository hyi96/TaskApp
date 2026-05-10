using System.Net;
using System.Net.Http.Json;

namespace TaskApp.Services;

public sealed class TaskAppCloudClient
{
    private readonly HttpClient _httpClient;

    public TaskAppCloudClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AccountResponse> CreateAccountAsync(string? displayName, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/accounts",
            new CreateAccountRequest(displayName),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Cloud API returned an empty account response.");
    }

    public async Task<IReadOnlyList<ProfileSummaryResponse>> ListProfilesAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<ProfileSummaryResponse>>(
            $"/api/accounts/{accountId}/profiles",
            cancellationToken) ?? new List<ProfileSummaryResponse>();
    }

    public async Task<ProfileSnapshotResponse> UploadProfileSnapshotAsync(
        Guid accountId,
        Guid profileId,
        string profileName,
        TaskAppDataSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"/api/accounts/{accountId}/profiles/{profileId}/snapshot",
            new UpsertProfileSnapshotRequest(profileName, snapshot),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProfileSnapshotResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Cloud API returned an empty profile snapshot response.");
    }

    public async Task<ProfileSnapshotResponse?> DownloadProfileSnapshotAsync(
        Guid accountId,
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/accounts/{accountId}/profiles/{profileId}/snapshot",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProfileSnapshotResponse>(cancellationToken);
    }
}
