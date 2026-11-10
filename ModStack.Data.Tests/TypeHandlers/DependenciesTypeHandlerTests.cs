using ModStack.Data.Repositories;
using System.Data;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using Xunit;

namespace ModStack.Data.Tests.TypeHandlers;

public class DependenciesTypeHandlerTests
{
    private readonly DependenciesTypeHandler _handler;

    public DependenciesTypeHandlerTests()
    {
        _handler = new DependenciesTypeHandler();
    }

    [Fact]
    public void SetValue_WithValidDependencies_ShouldSerializeCorrectly()
    {
        // Arrange
        var parameter = new SqliteParameter();
        var dependencies = new List<string> { "libssl", "libcrypto", "zlib" };

        // Act
        _handler.SetValue(parameter, dependencies);

        // Assert
        Assert.NotNull(parameter.Value);
        var jsonString = parameter.Value.ToString();
        Assert.NotNull(jsonString);
        
        var deserialized = JsonSerializer.Deserialize<List<string>>(jsonString);
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.Count);
        Assert.Contains("libssl", deserialized);
        Assert.Contains("libcrypto", deserialized);
        Assert.Contains("zlib", deserialized);
    }

    [Fact]
    public void SetValue_WithNullDependencies_ShouldSerializeEmptyArray()
    {
        // Arrange
        var parameter = new SqliteParameter();

        // Act
        _handler.SetValue(parameter, null);

        // Assert
        Assert.NotNull(parameter.Value);
        var jsonString = parameter.Value.ToString();
        Assert.Equal("[]", jsonString);
    }

    [Fact]
    public void SetValue_WithEmptyDependencies_ShouldSerializeEmptyArray()
    {
        // Arrange
        var parameter = new SqliteParameter();
        var dependencies = new List<string>();

        // Act
        _handler.SetValue(parameter, dependencies);

        // Assert
        Assert.NotNull(parameter.Value);
        var jsonString = parameter.Value.ToString();
        Assert.Equal("[]", jsonString);
    }

    [Fact]
    public void Parse_WithValidJsonArray_ShouldDeserializeCorrectly()
    {
        // Arrange
        var jsonString = """["mysql", "redis", "nginx"]""";

        // Act
        var result = _handler.Parse(jsonString);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains("mysql", result);
        Assert.Contains("redis", result);
        Assert.Contains("nginx", result);
    }

    [Fact]
    public void Parse_WithNullValue_ShouldReturnEmptyList()
    {
        // Act
        var result = _handler.Parse(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithDBNullValue_ShouldReturnEmptyList()
    {
        // Act
        var result = _handler.Parse(DBNull.Value);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithEmptyString_ShouldReturnEmptyList()
    {
        // Act
        var result = _handler.Parse("");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithEmptyJsonArray_ShouldReturnEmptyList()
    {
        // Act
        var result = _handler.Parse("[]");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithInvalidJson_ShouldReturnEmptyList()
    {
        // Act
        var result = _handler.Parse("invalid json");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithJsonObjectInsteadOfArray_ShouldReturnEmptyList()
    {
        // Act
        var result = _handler.Parse("""{"deps": ["mysql", "redis"]}""");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithArrayContainingNulls_ShouldFilterOutNulls()
    {
        // Arrange
        var jsonString = """["mysql", null, "redis", "", "nginx"]""";

        // Act
        var result = _handler.Parse(jsonString);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count); // null and empty string should be filtered out
        Assert.Contains("mysql", result);
        Assert.Contains("redis", result);
        Assert.Contains("nginx", result);
    }

    [Fact]
    public void Parse_WithArrayContainingNonStrings_ShouldHandleGracefully()
    {
        // Arrange
        var jsonString = """["mysql", 123, "redis", true, "nginx"]""";

        // Act
        var result = _handler.Parse(jsonString);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count); // Only strings should be included
        Assert.Contains("mysql", result);
        Assert.Contains("redis", result);
        Assert.Contains("nginx", result);
    }

    [Fact]
    public void DependenciesTypeHandler_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        var testDependencies = new List<string> 
        { 
            "libssl", 
            "libcrypto", 
            "zlib",
            "openssl",
            "libmysqlclient"
        };

        // Act
        var parameter = new SqliteParameter();
        _handler.SetValue(parameter, testDependencies);
        var serialized = parameter.Value?.ToString();
        var deserialized = _handler.Parse(serialized);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(testDependencies.Count, deserialized.Count);
        Assert.Equal(testDependencies, deserialized);
    }

    [Fact]
    public void DependenciesTypeHandler_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var specialDependencies = new List<string>
        {
            "libssl-1.1.1",
            "libcrypto@2.0",
            "zlib-1.2.11",
            "openssl/1.1.1",
            "libmysqlclient-dev",
            "pkg-config",
            "cmake>=3.10"
        };

        // Act
        var parameter = new SqliteParameter();
        _handler.SetValue(parameter, specialDependencies);
        var serialized = parameter.Value?.ToString();
        var deserialized = _handler.Parse(serialized);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(specialDependencies.Count, deserialized.Count);
        Assert.Equal(specialDependencies, deserialized);
    }

    [Fact]
    public void DependenciesTypeHandler_ShouldHandleLargeDependencyList()
    {
        // Arrange
        var largeDependencyList = new List<string>();
        for (int i = 0; i < 50; i++)
        {
            largeDependencyList.Add($"dependency-{i:000}");
        }

        // Act
        var parameter = new SqliteParameter();
        _handler.SetValue(parameter, largeDependencyList);
        var serialized = parameter.Value?.ToString();
        var deserialized = _handler.Parse(serialized);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(50, deserialized.Count);
        Assert.Equal(largeDependencyList, deserialized);
    }

    [Fact]
    public void DependenciesTypeHandler_ShouldHandleDuplicateDependencies()
    {
        // Arrange
        var dependenciesWithDuplicates = new List<string>
        {
            "mysql",
            "redis",
            "mysql", // duplicate
            "nginx",
            "redis", // duplicate
            "apache"
        };

        // Act
        var parameter = new SqliteParameter();
        _handler.SetValue(parameter, dependenciesWithDuplicates);
        var serialized = parameter.Value?.ToString();
        var deserialized = _handler.Parse(serialized);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(6, deserialized.Count); // Duplicates should be preserved
        Assert.Equal(dependenciesWithDuplicates, deserialized);
    }

    [Fact]
    public void DependenciesTypeHandler_ShouldHandleWhitespaceInDependencies()
    {
        // Arrange
        var dependenciesWithWhitespace = new List<string>
        {
            " mysql ",
            " redis ",
            "nginx",
            " apache "
        };

        // Act
        var parameter = new SqliteParameter();
        _handler.SetValue(parameter, dependenciesWithWhitespace);
        var serialized = parameter.Value?.ToString();
        var deserialized = _handler.Parse(serialized);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(4, deserialized.Count);
        Assert.Equal(dependenciesWithWhitespace, deserialized); // Whitespace should be preserved
    }
}
