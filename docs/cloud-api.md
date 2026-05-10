# Cloud API

`TaskApp.Api` is the first backend slice for proving cloud sync on desktop before Android work starts.

## Run Locally

```bash
dotnet run --project TaskApp.Api --urls http://localhost:5080
```

The default SQLite database is created under `TaskApp.Api/App_Data/` and is ignored by Git.

## Desktop Workflow

1. Start the API locally.
2. Open the desktop app.
3. Open Settings.
4. Keep the API URL as `http://localhost:5080`.
5. Click `Create Account`.
6. Click `Upload Profile` to push the current local profile snapshot.
7. Click `Download Profile` to pull the current profile snapshot back into the local store.

This is not login/logout yet. The temporary account ID represents one cloud account, and the existing desktop users remain profiles under that account.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/health` | API health check |
| `POST` | `/api/accounts` | Create a temporary account record |
| `GET` | `/api/accounts/{accountId}/profiles` | List profile snapshots under an account |
| `PUT` | `/api/accounts/{accountId}/profiles/{profileId}/snapshot` | Upload or replace a profile snapshot |
| `GET` | `/api/accounts/{accountId}/profiles/{profileId}/snapshot` | Download a profile snapshot |

## Storage Shape

The API stores accounts and profile snapshots in SQLite. Profile snapshots are persisted as separate JSON columns for user profile, tasks, rewards, tags, and logs. This keeps the first desktop-cloud round trip lossless while leaving room for a later row-level sync engine with revisions and conflict handling.
