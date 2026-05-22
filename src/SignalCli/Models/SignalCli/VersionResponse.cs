using Newtonsoft.Json;

namespace SignalCli.Models.SignalCli;

/// <summary>
/// Відповідь на запит версії Signal CLI.
/// </summary>
/// <remarks>
/// Містить інформацію про версію Signal CLI,
/// отриману викликом методу version.
/// </remarks>
/// <param name="Version">Рядок, що містить номер версії Signal CLI.</param>
public record VersionResponse(
    [property: JsonProperty("version")] string Version
);