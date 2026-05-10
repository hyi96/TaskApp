# Cloud API

`TaskApp.Api` is the first backend slice for proving cloud sync on desktop before Android work starts.

## Run Locally

```bash
dotnet run --project TaskApp.Api --urls http://localhost:5080
```

The default SQLite database is created under `TaskApp.Api/App_Data/` and is ignored by Git.

For production, set `TASKAPP_API_KEY`. Outside Development, the API refuses to start without it.

## Desktop Workflow

1. Start the API locally.
2. Open the desktop app.
3. Open Settings.
4. Keep the API URL as `http://localhost:5080`.
5. Set the API Key when the server requires one.
6. Click `Create Account`.
7. Click `Upload Profile` to push the current local profile snapshot.
8. Click `Download Profile` to pull the current profile snapshot back into the local store.

This is not login/logout yet. The temporary account ID represents one cloud account, and the existing desktop users remain profiles under that account.

## VPS Deployment

Use the Docker Compose stack in `deploy/vps/`.

```bash
cp deploy/vps/.env.example deploy/vps/.env
# Edit TASKAPP_DOMAIN and TASKAPP_API_KEY
docker compose --env-file deploy/vps/.env -f deploy/vps/docker-compose.yml up -d --build
```

Caddy terminates HTTPS and proxies to `TaskApp.Api`. The SQLite database is stored in a Docker volume.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/health` | API health check |
| `POST` | `/api/accounts` | Create a temporary account record |
| `GET` | `/api/accounts/{accountId}/profiles` | List profile snapshots under an account |
| `PUT` | `/api/accounts/{accountId}/profiles/{profileId}/snapshot` | Upload or replace a profile snapshot |
| `GET` | `/api/accounts/{accountId}/profiles/{profileId}/snapshot` | Download a profile snapshot |

All `/api` endpoints require `X-TaskApp-Api-Key` when `TASKAPP_API_KEY` is configured. `/health` stays public for uptime checks.

## Storage Shape

The API stores accounts and profile snapshots in SQLite. Profile snapshots are persisted as separate JSON columns for user profile, tasks, rewards, tags, and logs. This keeps the first desktop-cloud round trip lossless while leaving room for a later row-level sync engine with revisions and conflict handling.
