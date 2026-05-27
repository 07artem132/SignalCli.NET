# `ISignalGroups` — групи

CRUD над Signal-групами: список, join за invite-link, create/update (dual-mode), quit (idempotent).

Резолвиться як `host.Services.GetRequiredService<ISignalGroups>()`.

`groupId` усюди — base64-encoded group identifier (отримується з `ListGroupsAsync` або `UpdateGroupAsync` create-path).

---

## `ListGroupsAsync`

```csharp
Task<ListGroupsResponse> ListGroupsAsync(
    string account,
    CancellationToken cancellationToken = default);
```

Список усіх відомих груп акаунту. `ListGroupsResponse` реалізує `IReadOnlyList<Group>`.

**signal-cli RPC:** `ListGroupsCommand.java` @ `bda4e7fc`.

**Винятки:** `ArgumentNullException` якщо `account` — `null` чи порожній.

```csharp
var groups = await signalGroups.ListGroupsAsync("+380501234567");
foreach (var g in groups)
    Console.WriteLine($"{g.Name} (id={g.Id}, members={g.Members.Count})");
```

---

## `JoinGroupAsync`

```csharp
Task<JoinGroupResponse> JoinGroupAsync(
    string account,
    string uri,
    CancellationToken cancellationToken = default);
```

Приєднується до групи за invitation-посиланням (`signal.group/#...`).

**signal-cli RPC:** `JoinGroupCommand.java` @ `bda4e7fc`.

**§F13 трикутне розрізнення `OnlyRequested`:**
- `null` → direct join (повноправний member одразу);
- `true` → pending admin approval;
- `false` → ніколи не повертається upstream'ом для цього поля.

```csharp
var resp = await signalGroups.JoinGroupAsync(
    account: "+380501234567",
    uri: "https://signal.group/#CjQK...");
if (resp.OnlyRequested == true)
    Console.WriteLine("Чекаємо схвалення admin'а.");
else
    Console.WriteLine($"Joined, groupId = {resp.GroupId}");
```

**Винятки:** `ArgumentException` якщо `account`/`uri` порожні; `JsonRpcException` для invalid link / unknown version / inactive / pending-admin-approval (`-1`) або IO (`-3`).

---

## `UpdateGroupAsync`

```csharp
Task<UpdateGroupResponse> UpdateGroupAsync(
    UpdateGroupOptions options,
    CancellationToken cancellationToken = default);
```

**Dual-mode (§F14):** якщо `options.GroupId == null` → upstream викликає `createGroup` спочатку, далі `updateGroup` з рештою полів. У create-path `UpdateGroupResponse.GroupId` присутній; у update-path — `null` (вже відомий).

**signal-cli RPC:** `UpdateGroupCommand.java` @ `bda4e7fc`. **§F10 IOException divergence:** мапиться на `-32603 INTERNAL_ERROR`, не на `-3 IoError`.

**Builder API** — усі поля опційні; будуй потрібну mutation:

```csharp
// CREATE — без GroupId:
var createOpts = new UpdateGroupOptions.Builder(account: "+380501234567")
    .WithName("Робочий чат")
    .WithDescription("Daily standups")
    .WithMembers(["+380509999999", "+380508888888"])
    .Build();
var created = await signalGroups.UpdateGroupAsync(createOpts);
string newGroupId = created.GroupId!;  // тільки на create-path

// UPDATE — з GroupId:
var updateOpts = new UpdateGroupOptions.Builder(account: "+380501234567")
    .WithGroupId(newGroupId)
    .WithAdmins(["+380509999999"])
    .WithExpiration(86400)                              // 24h disappearing messages
    .WithPermissionAddMember(GroupPermission.OnlyAdmins)
    .WithLinkState(GroupLinkState.EnabledWithApproval)
    .Build();
await signalGroups.UpdateGroupAsync(updateOpts);
```

**Builder методи:** `WithGroupId`, `WithName`, `WithDescription`, `WithAvatarPath`, `WithMembers`/`WithRemoveMembers`, `WithAdmins`/`WithRemoveAdmins`, `WithBans`/`WithUnbans`, `WithResetLink`, `WithLinkState`, `WithPermissionAddMember`/`WithPermissionEditDetails`/`WithPermissionSendMessages`, `WithExpiration(seconds)`.

**Enums:**
- `GroupPermission`: `EveryMember` | `OnlyAdmins`.
- `GroupLinkState`: `Disabled` | `Enabled` | `EnabledWithApproval`.

**Винятки:** `ArgumentNullException` якщо `options` — `null`; `GroupAdminRequiredException` (`-1` + "admin") при mutating операції з non-admin акаунту.

---

## `QuitGroupAsync`

```csharp
Task<QuitGroupResponse> QuitGroupAsync(
    string account,
    string groupId,
    bool deleteLocally = false,
    IEnumerable<string>? admins = null,
    CancellationToken cancellationToken = default);
```

Залишає групу. **§F8 idempotent:** якщо акаунт вже не member — повертає response з `WasAlreadyNotMember = true` замість throw. Реалізує CLAUDE.md rule #14 (idempotency over exceptions).

`deleteLocally` = `true` → видалити group-data локально після quit-send (на wire — поле `"delete"`).

`admins` потрібен якщо акаунт — last admin групи (передаєш successor'ів).

**signal-cli RPC:** `QuitGroupCommand.java` @ `bda4e7fc`.

```csharp
var resp = await signalGroups.QuitGroupAsync(
    account: "+380501234567",
    groupId: "base64GroupId",
    deleteLocally: true);
if (resp.WasAlreadyNotMember)
    Console.WriteLine("Вже не member — no-op.");
```

**Винятки:** `ArgumentException` якщо `account`/`groupId` порожні; `JsonRpcException` для invalid group / last-admin-without-successor.
