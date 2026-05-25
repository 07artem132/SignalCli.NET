## ADDED Requirements

### Requirement: New `ISignalStickers` interface SHALL be added with 3 methods

A new `public interface SignalCli.Interfaces.Signal.ISignalStickers` SHALL be registered as singleton via `AddSignalCli`:

```csharp
Task<ListStickerPacksResponse> ListStickerPacksAsync(string account, CancellationToken ct = default);
Task<UploadStickerPackResponse> UploadStickerPackAsync(string account, string path, CancellationToken ct = default);
Task AddStickerPackAsync(string account, string packId, string packKey, CancellationToken ct = default);
```

Each method invokes the matching signal-cli JSON-RPC method (`listStickerPacks`, `uploadStickerPack`, `addStickerPack`).

#### Scenario: List installed sticker packs
- **GIVEN** an account with sticker packs installed
- **WHEN** `await stickers.ListStickerPacksAsync("+1")` is invoked
- **THEN** RPC method `"listStickerPacks"` is called
- **AND** response is `IReadOnlyList<StickerPack>` with each pack's `PackId`, `PackKey`, `Url`, `Title`, `Author`, `Installed`

#### Scenario: Upload a sticker pack from a manifest directory
- **GIVEN** `path = "/tmp/my-pack/"` containing `manifest.json` + sticker images
- **WHEN** `UploadStickerPackAsync("+1", path)` is invoked
- **THEN** RPC method `"uploadStickerPack"` is called with `path`
- **AND** `UploadStickerPackResponse.PackId` + `PackKey` + `Url` returned by signal-cli are populated
- **AND** the URL can be shared with other users for them to install

#### Scenario: Install a sticker pack by ID + key
- **GIVEN** `packId = "abc123..."`, `packKey = "xyz789..."` from a sharing URL
- **WHEN** `AddStickerPackAsync("+1", packId, packKey)` is invoked
- **THEN** RPC method `"addStickerPack"` is called
- **AND** pack appears in subsequent `ListStickerPacksAsync` results with `Installed = true`

### Requirement: `ISignalEventService` SHALL expose sticker-pack-install sync event stream

`ISignalEventService` SHALL gain one new event-stream pair (IObservable + IAsyncEnumerable per RG06):

- `IObservable<StickerPackInstallEventArgs> StickerPackInstalls`
- `IAsyncEnumerable<StickerPackInstallEventArgs> StickerPackInstallsAsync(CancellationToken ct = default)`

`JsonStickerPackOperation` DTO SHALL be re-engineered from upstream `org.asamk.signal.json.JsonSyncMessage.java` per §0.5 source-of-truth protocol. The event fires from `syncMessage` envelope, dispatched via `DispatchSyncMessage` (existing path, not the data-message path).

#### Scenario: Receive a sticker-pack-install sync event from a linked device
- **GIVEN** secondary device installed a sticker pack via `addStickerPack`
- **WHEN** primary device receives `syncMessage.stickerPackOperations = [{packId, packKey, type: "INSTALL"}]`
- **THEN** `StickerPackInstalls` IObservable emits one `StickerPackInstallEventArgs` per operation
- **AND** consumer can refresh local sticker-pack cache
