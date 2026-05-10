# Cloud API

`TaskApp.Api` is the backend slice for proving cloud sync on desktop first, then downloading those profiles from Android.

## Run Locally

```bash
dotnet run --project TaskApp.Api --urls http://localhost:5080
```

The default SQLite database is created under `TaskApp.Api/App_Data/` and is ignored by Git.

For production, set `TASKAPP_API_KEY`. Outside Development, the API refuses to start without it. That key is only for creating accounts/admin access; clients use the generated account secret after account creation.

## Desktop Workflow

1. Start the API locally or use `https://taskapp-api.hyi96.dev`.
2. Open the desktop app.
3. Open Settings.
4. Set the API URL.
5. Set the server API key when creating a new account on the VPS.
6. Click `Create Account`; the desktop app stores the returned account ID and account secret.
7. Click `Login` to verify the account ID and account secret.
8. Click `Upload All Profiles` to push every local desktop user profile under the same cloud account.
9. Use `Upload Profile` or `Download Profile` for only the current desktop profile.

The desktop user switch still switches local profiles under the same cloud account. Login/logout is account-level and can be expanded later.

## Android Workflow

1. Install the APK.
2. Enter the same API URL, account ID, and account secret from desktop Settings.
3. Tap `Login`.
4. Tap `List profiles`; the first profile ID is filled automatically if the field is empty.
5. Tap `Download profile` to confirm the phone can read the desktop-uploaded snapshot.

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
| `POST` | `/api/accounts` | Create an account and return its one-time visible login secret |
| `POST` | `/api/accounts/login` | Verify account ID and account secret |
| `GET` | `/api/accounts/{accountId}/profiles` | List profile snapshots under an account |
| `PUT` | `/api/accounts/{accountId}/profiles/{profileId}/snapshot` | Upload or replace a profile snapshot |
| `GET` | `/api/accounts/{accountId}/profiles/{profileId}/snapshot` | Download a profile snapshot |

`POST /api/accounts` requires `X-TaskApp-Api-Key` when `TASKAPP_API_KEY` is configured. Profile endpoints accept either that server API key or the account's `X-TaskApp-Account-Secret`. `/health` stays public for uptime checks.

## Storage Shape

The API stores accounts and profile snapshots in SQLite. Profile snapshots are persisted as separate JSON columns for user profile, tasks, rewards, tags, and logs. This keeps the first desktop-cloud round trip lossless while leaving room for a later row-level sync engine with revisions and conflict handling.
