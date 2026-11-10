using System.Text.Json.Serialization;
using ModStack.Common.Enums;

namespace ModStack.GUI.Services.SeedData;

/// <summary>
/// Strongly typed DTO for seed package data
/// </summary>
public class SeedPackage
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("isPopular")]
    public bool IsPopular { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("packageType")]
    public PackageType PackageType { get; set; } = PackageType.Service;

    [JsonPropertyName("versions")]
    public List<SeedPackageVersion> Versions { get; set; } = new();
}

/// <summary>
/// Strongly typed DTO for seed package version data
/// </summary>
public class SeedPackageVersion
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("releaseDate")]
    public DateTime ReleaseDate { get; set; }

    [JsonPropertyName("isStable")]
    public bool IsStable { get; set; }

    [JsonPropertyName("isRecommended")]
    public bool IsRecommended { get; set; }

    [JsonPropertyName("isLTS")]
    public bool IsLTS { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("downloadUrls")]
    public List<string> DownloadUrls { get; set; } = new();
}

/// <summary>
/// Root container for seed data file
/// </summary>
public class SeedDataFile
{
    [JsonPropertyName("packages")]
    public List<SeedPackage> Packages { get; set; } = new();
}