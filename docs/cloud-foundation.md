# Cloud Foundation

This branch establishes the first cloud/mobile boundary without changing desktop behavior.

## Current Shape

- `TaskApp.Core` owns domain models, serialization DTOs, mappers, store interfaces, and the full-data snapshot contract.
- `TaskApp` owns Avalonia desktop UI and local infrastructure.
- `TaskApp.Tests` references both projects and continues to validate desktop behavior.

## Shared Contracts

`ITaskAppDataStore` is the main data boundary. View models use it for tasks, rewards, tags, profile data, logs, emergency save, activity-duration queries, and log merge/undo support.

`ILocalUserCatalog` preserves the current local multi-user behavior. `IUserCatalog` is the narrower account boundary that a future cloud-backed identity system can implement.

`TaskAppDataSnapshot` gives API/import/sync work a single canonical payload for a user's current data.

## Next Milestone

The next branch should add `TaskApp.Api` and map `TaskAppDataSnapshot` plus the existing DTOs into a server-side database model. Keep desktop behavior unchanged until the API can import a snapshot and return it losslessly.
