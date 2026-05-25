## ADDED Requirements

### Requirement: `ISignalDevices` SHALL expose 4 primary-perspective device-management methods

In addition to the existing secondary-perspective `StartLinkAsync` / `FinishLinkAsync` (which link THIS device as a secondary to an existing primary), `ISignalDevices` SHALL expose four new methods called FROM the primary device:

```csharp
Task AddDeviceAsync(string account, string uri, CancellationToken ct = default);
Task<ListDevicesResponse> ListDevicesAsync(string account, CancellationToken ct = default);
Task RemoveDeviceAsync(string account, long deviceId, CancellationToken ct = default);
Task UpdateDeviceAsync(string account, long deviceId, string deviceName, CancellationToken ct = default);
```

Each method invokes the matching signal-cli JSON-RPC method (`addDevice`, `listDevices`, `removeDevice`, `updateDevice`). XMLDoc on each MUST explicitly state the perspective (primary vs secondary) to avoid confusion with existing `StartLinkAsync`/`FinishLinkAsync`.

#### Scenario: Primary registers a new secondary via received URI
- **GIVEN** the secondary device emitted a `sgnl://linkdevice?uuid=...&pub_key=...` URI (scanned from QR)
- **WHEN** primary calls `await devices.AddDeviceAsync("+1", "sgnl://linkdevice?...")` 
- **THEN** RPC method `"addDevice"` is invoked
- **AND** secondary device receives provisioning and becomes a linked device

#### Scenario: List all linked devices from primary
- **GIVEN** account "+1" with 2 linked secondary devices
- **WHEN** `await devices.ListDevicesAsync("+1")` is invoked
- **THEN** RPC method `"listDevices"` is invoked
- **AND** response includes 3 entries (primary + 2 secondaries), each with `Id`, `Name`, `Created`, `LastSeen`

#### Scenario: Remove a linked device
- **GIVEN** a linked device with `deviceId = 2`
- **WHEN** `RemoveDeviceAsync("+1", 2)` is invoked
- **THEN** RPC method `"removeDevice"` is invoked with `deviceId: 2`
- **AND** subsequent `ListDevicesAsync` no longer contains that entry
- **AND** the removed device receives a logout notification

#### Scenario: Rename a linked device
- **GIVEN** linked device with `deviceId = 2, name = "Old Phone"`
- **WHEN** `UpdateDeviceAsync("+1", 2, "iPhone 16")` is invoked
- **THEN** RPC method `"updateDevice"` is invoked with `deviceId: 2, deviceName: "iPhone 16"`
- **AND** subsequent `ListDevicesAsync` shows the updated name
