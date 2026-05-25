## ADDED Requirements

### Requirement: `ISignalGroups` SHALL expose `JoinGroupAsync`

A `Task<JoinGroupResponse> JoinGroupAsync(string account, string uri, CancellationToken ct = default)` method SHALL invoke signal-cli's `joinGroup` JSON-RPC method with the invitation URI (`https://signal.group/#...`).

#### Scenario: Joining a group via invitation URI
- **GIVEN** `account = "+1"`, `uri = "https://signal.group/#CjQKII..."`
- **WHEN** consumer calls `await groups.JoinGroupAsync(account, uri)`
- **THEN** RPC method `"joinGroup"` is invoked
- **AND** `JoinGroupResponse.GroupId` is populated from server response

#### Scenario: Invalid URI returns server-side error
- **GIVEN** malformed `uri = "not-a-url"`
- **WHEN** `JoinGroupAsync` is invoked
- **THEN** signal-cli returns `error.code = -1` and the task faults with `JsonRpcException`

### Requirement: `ISignalGroups` SHALL expose `UpdateGroupAsync`

A `Task<UpdateGroupResponse> UpdateGroupAsync(UpdateGroupOptions options, CancellationToken ct = default)` method SHALL invoke `updateGroup`. `UpdateGroupOptions` SHALL be a `sealed record` with a `Builder` providing 12+ optional fields: `Name`, `Description`, `AvatarPath`, `AddMembers`/`RemoveMembers`, `AddAdmins`/`RemoveAdmins`, `RemoveBanned`, `AddBanned`, `ExpirationSeconds`, `LinkState` (enum: `Enabled` | `EnabledWithApproval` | `Disabled`), `AddMemberPermission` / `EditDetailsPermission` (enum: `EveryMember` | `OnlyAdmins`), `Description`.

For new groups (no `GroupId` passed), `UpdateGroupAsync` creates; for existing groups, modifies.

#### Scenario: Creating a new group with members
- **GIVEN** `UpdateGroupOptions(Account: "+1", Name: "My Group", AddMembers: ["+2", "+3"])` (no `GroupId`)
- **WHEN** `UpdateGroupAsync` is invoked
- **THEN** RPC method `"updateGroup"` is called with `name` and `member` array
- **AND** `UpdateGroupResponse.GroupId` returns the newly-created group id

#### Scenario: Renaming an existing group
- **GIVEN** existing `GroupId`, and `UpdateGroupOptions(Account: "+1", GroupId: "<id>", Name: "Renamed")`
- **WHEN** `UpdateGroupAsync` is invoked
- **THEN** RPC `updateGroup` is called with `groupId` + `name` only
- **AND** other group attributes remain unchanged on server

#### Scenario: Non-admin tries to change group settings
- **GIVEN** caller is not an admin and `UpdateGroupOptions` modifies `Name`
- **WHEN** signal-cli responds with `{"error":{"code":-1,"message":"...admin..."}}`
- **THEN** the task faults with `GroupAdminRequiredException` (from `messaging-interactive` cross-cutting)

### Requirement: `ISignalGroups` SHALL expose `QuitGroupAsync`

A `Task QuitGroupAsync(string account, string groupId, QuitGroupBehavior behavior = QuitGroupBehavior.LeaveOnly, CancellationToken ct = default)` method SHALL invoke `quitGroup`. `QuitGroupBehavior.Delete` adds `delete: true` to the wire payload (also removes local group data per signal-cli `--delete` semantics).

#### Scenario: Leave group without local deletion
- **GIVEN** `behavior = QuitGroupBehavior.LeaveOnly`
- **WHEN** `QuitGroupAsync` is invoked
- **THEN** RPC payload contains `delete: false`
- **AND** group remains in `ListGroupsAsync` results with `IsMember = false`

#### Scenario: Leave group and delete local data
- **GIVEN** `behavior = QuitGroupBehavior.Delete`
- **WHEN** `QuitGroupAsync` is invoked
- **THEN** RPC payload contains `delete: true`
- **AND** group disappears from `ListGroupsAsync` results
