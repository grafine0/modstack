using System;
using System.Text.Json.Serialization;

namespace ModStack.Common.Models;

public class UpdateInfo
{
    [JsonPropertyName("isUpdateAvailable")]
    public bool IsUpdateAvailable { get; set; }

    [JsonPropertyName("latestVersion")]
    public string LatestVersion { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("checksum")]
    public string Checksum { get; set; } = string.Empty;

    [JsonPropertyName("releaseNotes")]
    public string ReleaseNotes { get; set; } = string.Empty;
}
