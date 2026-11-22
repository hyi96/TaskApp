# Cloud API

`TaskApp.Api` stores accounts and profile snapshots for the desktop and Android apps. A single account can hold multiple profiles.

## Run Locally

```bash
dotnet run --project TaskApp.Api/TaskApp.Api.csproj -- --urls http://localhost:5080
```

The default SQLite database is created under `TaskApp.Api/App_Data/` and is ignored by Git.

For production, set `TASKAPP_API_KEY`. Outside Development, the API refuses to start without it. That key is only for creating accounts/admin access; clients use the generated account secret after account creation.

## Desktop Workflow

1. Open the Settings gear and select **Cloud Sync**.
2. Set the **API URL** to your server, the default `https://taskapp-api.hyi96.dev`, or the local development API.
3. For a new account on a production server, enter the server API key and click **Create Account**. The desktop app saves the account ID and account secret from the response.
4. For an existing account, enter its account ID and account secret. Click **Login** to verify them. The Cloud login indicator shows the verification state.
5. Click **Upload Profile** for the active local profile or **Upload All Profiles** for every local profile. **Download Profile** asks for confirmation before replacing the active local profile with its cloud snapshot.

Use **Copy** beside the Account Secret field to transfer the secret securely to your phone. The server API key is for server administration and account creation. The Android app uses the account ID and account secret.

## Android Workflow

Follow the [Android sign-in guide](android-apk.md#sign-in-and-switch-profiles). The **Profile** picker switches profiles within the logged-in account. On desktop, the user switch selects a local profile under the same cloud account.

Each upload replaces the stored snapshot for that profile. Changes from multiple devices are not merged. Download the latest snapshot before editing on another device.

## VPS Deployment

Follow the [VPS deployment guide](../deploy/vps/README.md). Caddy terminates HTTPS and proxies to `TaskApp.Api`. The SQLite database is stored in a Docker volume.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/health` | API reachability and SQLite connectivity check |
| `POST` | `/api/accounts` | Create an account and return its one-time visible login secret |
| `POST` | `/api/accounts/login` | Verify account ID and account secret |
| `GET` | `/api/accounts/{accountId}/profiles` | List profile snapshots under an account |
| `PUT` | `/api/accounts/{accountId}/profiles/{profileId}/snapshot` | Upload or replace a profile snapshot |
| `GET` | `/api/accounts/{accountId}/profiles/{profileId}/snapshot` | Download a profile snapshot |

`POST /api/accounts` requires `X-TaskApp-Api-Key` when `TASKAPP_API_KEY` is configured. Profile endpoints accept either that server API key or the account's `X-TaskApp-Account-Secret`. `/health` stays public for uptime checks.

## Storage Shape

The API stores accounts and profile snapshots in SQLite. Profile snapshots are persisted as separate JSON columns for user profile, tasks, rewards, tags, and logs. This keeps the first desktop-cloud round trip lossless while leaving room for a later row-level sync engine with revisions and conflict handling.
