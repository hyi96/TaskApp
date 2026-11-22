# Cloud Foundation

TaskApp shares profile data between desktop and Android through an optional Cloud API. The desktop app keeps its local data store and uses snapshots for explicit transfers.

## Current Shape

- `TaskApp.Core` owns domain models, serialization DTOs, mappers, store interfaces, and the full-data snapshot contract.
- `TaskApp` owns Avalonia desktop UI and local infrastructure.
- `TaskApp.Api` stores accounts and profile snapshots in SQLite.
- `TaskApp.Android` downloads profiles and caches snapshots on the device.
- `TaskApp.Tests` exercises the desktop app, shared contracts, and API storage.

## Shared Contracts

`ITaskAppDataStore` is the main data boundary. View models use it for tasks, rewards, tags, profile data, logs, emergency save, activity-duration queries, and log merge/undo support.

`ILocalUserCatalog` preserves the current local multi-user behavior. `IUserCatalog` is the narrower account boundary that a future cloud-backed identity system can implement.

`TaskAppDataSnapshot` is the complete payload for one profile. Desktop and Android use `TaskAppCloudClient` to send and receive it.

## Transfer Behavior

A cloud account can have several profiles. Uploading replaces the stored snapshot for the chosen profile. See the [Cloud API guide](cloud-api.md) for account setup and desktop sync, or the [Android guide](android-apk.md) for the phone workflow.
