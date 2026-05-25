using System.Text.Json;
using SignalCli.Models.Signal.Devices;
using SignalCli.Serialization;

namespace SignalCli.Tests.Serialization;

/// <summary>
/// signal-cli-api-coverage Wave 5 (device-management): wire-shape pinning.
/// </summary>
public class DeviceManagementSerializationTests
{
    [Fact]
    public void Device_HasExactlyFourFields_NoIsThisDevice()
    {
        // §F6: wire shape — 4 fields only. Upstream Device record має 5, але JsonDevice projection
        // drop'ає isThisDevice. Якщо хтось додасть IsThisDevice — wire-shape брехня.
        var fields = typeof(Device).GetProperties()
            .Where(p => p.Name != "EqualityContract") // record default property
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(["CreatedTimestamp", "Id", "LastSeenTimestamp", "Name"], fields);
    }

    [Fact]
    public void Device_IdIsLong_NotInt()
    {
        // §int → long widening на wire (JsonDevice.id : long, не int).
        var idProp = typeof(Device).GetProperty("Id")!;
        Assert.Equal(typeof(long), idProp.PropertyType);
    }

    [Fact]
    public void RemoveDeviceParameters_DeviceIdIsInt()
    {
        // §wire-asymmetry: input — int (argparse type(int.class)); output (Device.Id) — long.
        var deviceIdProp = typeof(RemoveDeviceParameters).GetProperty("DeviceId")!;
        Assert.Equal(typeof(int), deviceIdProp.PropertyType);
    }

    [Fact]
    public void UpdateDeviceParameters_DeviceIdIsInt()
    {
        var deviceIdProp = typeof(UpdateDeviceParameters).GetProperty("DeviceId")!;
        Assert.Equal(typeof(int), deviceIdProp.PropertyType);
    }

    [Fact]
    public void ListDevicesResponse_DeserializesFromFlatArray()
    {
        const string json = """
        [
          {"id":1,"name":"My phone","createdTimestamp":1717181920000,"lastSeenTimestamp":1717200000000},
          {"id":2,"name":"Desktop","createdTimestamp":1717200000000,"lastSeenTimestamp":0}
        ]
        """;
        var resp = JsonSerializer.Deserialize(json, SignalJsonContext.Default.ListDevicesResponse)!;
        Assert.Equal(2, resp.Count);
        Assert.Equal(1L, resp[0].Id);  // primary
        Assert.Equal("My phone", resp[0].Name);
        Assert.Equal(0L, resp[1].LastSeenTimestamp); // ніколи не connect'ився
    }

    [Fact]
    public void Device_Name_CanBeNull()
    {
        const string json = """{"id":3,"name":null,"createdTimestamp":1,"lastSeenTimestamp":1}""";
        var dev = JsonSerializer.Deserialize(json, SignalJsonContext.Default.Device)!;
        Assert.Null(dev.Name);
    }

    [Fact]
    public void AddDeviceParameters_HasAccountAndUri()
    {
        var p = new AddDeviceParameters("+1", "sgnl://linkdevice?uuid=abc&pub_key=xyz");
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.AddDeviceParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("+1", doc.RootElement.GetProperty("account").GetString());
        Assert.Contains("sgnl://linkdevice", doc.RootElement.GetProperty("uri").GetString());
    }

    [Fact]
    public void UpdateDeviceParameters_HasAllThreeFields()
    {
        var p = new UpdateDeviceParameters("+1", 42, "New Device Name");
        var json = JsonSerializer.Serialize(p, SignalJsonContext.Default.UpdateDeviceParameters);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(42, doc.RootElement.GetProperty("deviceId").GetInt32());
        Assert.Equal("New Device Name", doc.RootElement.GetProperty("deviceName").GetString());
    }
}
