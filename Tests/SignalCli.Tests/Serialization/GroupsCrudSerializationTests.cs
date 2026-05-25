using System.Text.Json;
using SignalCli.Models.Signal.Groups;
using SignalCli.Serialization;

namespace SignalCli.Tests.Serialization;

/// <summary>
/// signal-cli-api-coverage Wave 2 (groups-crud): wire-shape pinning. Тести парсять через
/// <see cref="JsonDocument"/> щоб уникнути sensitivity до STJ-encoder defaults.
/// </summary>
public class GroupsCrudSerializationTests
{
    private static JsonDocument SerializeAndParse<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        return JsonDocument.Parse(json);
    }

    // ---- joinGroup ----

    [Fact]
    public void JoinGroupParameters_HasCamelCaseFields()
    {
        var p = new JoinGroupParameters("+380501234567", "https://signal.group/#CjQKIA...");
        using var doc = SerializeAndParse(p, SignalJsonContext.Default.JoinGroupParameters);
        var root = doc.RootElement;

        Assert.Equal("+380501234567", root.GetProperty("account").GetString());
        Assert.Equal("https://signal.group/#CjQKIA...", root.GetProperty("uri").GetString());
    }

    [Fact]
    public void JoinGroupResponse_DirectJoin_OmitsOnlyRequested()
    {
        // §F13: direct join → onlyRequested ВІДСУТНІЙ у JSON (не "false").
        var resp = new JoinGroupResponse(
            TimeStamp: 1717181920000L,
            Results: null,
            GroupId: "Z3JvdXBJZA==",
            OnlyRequested: null);

        using var doc = SerializeAndParse(resp, SignalJsonContext.Default.JoinGroupResponse);
        var root = doc.RootElement;

        Assert.Equal(1717181920000L, root.GetProperty("timestamp").GetInt64());
        Assert.Equal("Z3JvdXBJZA==", root.GetProperty("groupId").GetString());
        // CRITICAL: поле має бути ВІДСУТНІМ, не присутнім зі значенням false.
        Assert.False(root.TryGetProperty("onlyRequested", out _));
    }

    [Fact]
    public void JoinGroupResponse_PendingApproval_HasOnlyRequestedTrue()
    {
        // §F13: pending approval → onlyRequested:true.
        var resp = new JoinGroupResponse(
            TimeStamp: 42L,
            Results: null,
            GroupId: "Z3JvdXBJZA==",
            OnlyRequested: true);

        using var doc = SerializeAndParse(resp, SignalJsonContext.Default.JoinGroupResponse);
        Assert.True(doc.RootElement.GetProperty("onlyRequested").GetBoolean());
    }

    [Fact]
    public void JoinGroupResponse_Deserializes_DirectJoin_PreservesNullOnlyRequested()
    {
        // Wire-format direct join (onlyRequested відсутній) → DTO має OnlyRequested == null.
        const string json = """{"timestamp":42,"results":null,"groupId":"Z3JvdXBJZA=="}""";
        var resp = JsonSerializer.Deserialize(json, SignalJsonContext.Default.JoinGroupResponse)!;
        Assert.Null(resp.OnlyRequested);
        Assert.Equal("Z3JvdXBJZA==", resp.GroupId);
    }

    // ---- updateGroup ----

    [Fact]
    public void UpdateGroupParameters_HasCamelCaseFields_AndKebabEnumValues()
    {
        var p = new UpdateGroupParameters(
            Account: "+1",
            GroupId: "g==",
            Name: "new name",
            Description: null,
            Avatar: null,
            Members: ["+2"],
            RemoveMembers: null,
            Admins: null,
            RemoveAdmins: null,
            Bans: null,
            Unbans: null,
            ResetLink: false,
            Link: "enabled-with-approval",
            SetPermissionAddMember: "only-admins",
            SetPermissionEditDetails: null,
            SetPermissionSendMessages: null,
            Expiration: 3600);

        using var doc = SerializeAndParse(p, SignalJsonContext.Default.UpdateGroupParameters);
        var root = doc.RootElement;

        // Wave-2 plural-camelCase per JsonRpcNamespace conversion.
        Assert.Equal("g==", root.GetProperty("groupId").GetString());
        Assert.Equal("new name", root.GetProperty("name").GetString());
        Assert.Equal("+2", root.GetProperty("members")[0].GetString());

        // Enum values — kebab-case wire form.
        Assert.Equal("enabled-with-approval", root.GetProperty("link").GetString());
        Assert.Equal("only-admins", root.GetProperty("setPermissionAddMember").GetString());

        Assert.Equal(3600, root.GetProperty("expiration").GetInt32());
        Assert.False(root.GetProperty("resetLink").GetBoolean());
    }

    [Fact]
    public void UpdateGroupResponse_CreatePath_HasGroupId()
    {
        // §F14: create-path → groupId present у response.
        var resp = new UpdateGroupResponse(TimeStamp: 1L, Results: null, GroupId: "Z3JvdXBJZA==");
        using var doc = SerializeAndParse(resp, SignalJsonContext.Default.UpdateGroupResponse);
        Assert.Equal("Z3JvdXBJZA==", doc.RootElement.GetProperty("groupId").GetString());
    }

    [Fact]
    public void UpdateGroupResponse_UpdatePath_OmitsGroupId()
    {
        // §F14: update existing group → groupId absent (not null-string).
        var resp = new UpdateGroupResponse(TimeStamp: 1L, Results: null, GroupId: null);
        using var doc = SerializeAndParse(resp, SignalJsonContext.Default.UpdateGroupResponse);
        Assert.False(doc.RootElement.TryGetProperty("groupId", out _));
    }

    // ---- quitGroup ----

    [Fact]
    public void QuitGroupParameters_HasDeleteField()
    {
        var p = new QuitGroupParameters("+1", "g==", Delete: true, Admins: ["+2"]);
        using var doc = SerializeAndParse(p, SignalJsonContext.Default.QuitGroupParameters);
        var root = doc.RootElement;

        Assert.Equal("+1", root.GetProperty("account").GetString());
        Assert.Equal("g==", root.GetProperty("groupId").GetString());
        Assert.True(root.GetProperty("delete").GetBoolean());
        Assert.Equal("+2", root.GetProperty("admins")[0].GetString());
    }

    [Fact]
    public void QuitGroupResponse_IdempotentEmpty_Deserializes_WithBothNull()
    {
        // §F8: NotAGroupMemberException → upstream повертає {} → DTO повинен deserialize'итись без помилки.
        const string json = "{}";
        var resp = JsonSerializer.Deserialize(json, SignalJsonContext.Default.QuitGroupResponse)!;
        Assert.Null(resp.TimeStamp);
        Assert.Null(resp.Results);
        Assert.True(resp.WasAlreadyNotMember);
    }

    [Fact]
    public void QuitGroupResponse_NormalQuit_HasTimestampAndResults()
    {
        // Звичайний quit-path → timestamp+results присутні.
        const string json = """{"timestamp":1717181920000,"results":[]}""";
        var resp = JsonSerializer.Deserialize(json, SignalJsonContext.Default.QuitGroupResponse)!;
        Assert.Equal(1717181920000L, resp.TimeStamp);
        Assert.NotNull(resp.Results);
        Assert.False(resp.WasAlreadyNotMember);
    }
}
