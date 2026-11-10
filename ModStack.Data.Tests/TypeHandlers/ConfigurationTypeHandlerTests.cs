using ModStack.Data.Repositories;
using System.Data;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using Xunit;

namespace ModStack.Data.Tests.TypeHandlers;

public class ConfigurationTypeHandlerTests
{
    private readonly ConfigurationTypeHandler _handler;

    public ConfigurationTypeHandlerTests()
    {
        _handler = new ConfigurationTypeHandler();
    }

    [Fact]
    public void SetValue_WithValidConfiguration_ShouldSerializeCorrectly()
    {
        // Arrange
        var parameter = new SqliteParameter();
        var configuration = new Dictionary<string, object>
        {
            { "port", 8080 },
            { "host", "localhost" },
            { "enabled", true }
        };

        // Act
        _handler.SetValue(parameter, configuration);

        // Assert
        Assert.NotNull(parameter.Value);
        var jsonString = parameter.Value.ToString();
        Assert.NotNull(jsonString);
        
        var deserialized = _handler.Parse(jsonString);
        Assert.NotNull(deserialized);
        Assert.Equal(8080, deserialized["port"]);
        Assert.Equal("localhost", deserialized["host"]);
        Assert.True((bool)deserialized["enabled"]);
    }

    [Fact]
    public void SetValue_WithNullConfiguration_ShouldSerializeEmptyObject()
    {
        // Arrange
        var parameter = new SqliteParameter();

        // Act
        _handler.SetValue(parameter, null);

        // Assert
        Assert.NotNull(parameter.Value);
        var jsonString = parameter.Value.ToString();
        Assert.Equal("{}", jsonString);
    }

    [Fact]
    public void Parse_WithValidJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var jsonString = """{"port": 3306, "database": "testdb", "ssl": true}""";

        // Act
        var result = _handler.Parse(jsonString);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(3306, result["port"]);
        Assert.Equal("testdb", result["database"]);
        Assert.True((bool)result["ssl"]);
    }

    [Fact]
    public void Parse_WithNullValue_ShouldReturnEmptyDictionary()
    {
        // Act
        var result = _handler.Parse(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithDBNullValue_ShouldReturnEmptyDictionary()
    {
        // Act
        var result = _handler.Parse(DBNull.Value);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithEmptyString_ShouldReturnEmptyDictionary()
    {
        // Act
        var result = _handler.Parse("");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithEmptyJsonObject_ShouldReturnEmptyDictionary()
    {
        // Act
        var result = _handler.Parse("{}");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithInvalidJson_ShouldReturnEmptyDictionary()
    {
        // Act
        var result = _handler.Parse("invalid json");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithComplexJsonStructure_ShouldHandleCorrectly()
    {
        // Arrange
        var complexJson = """
        {
            "database": {
                "host": "localhost",
                "port": 5432,
                "credentials": {
                    "username": "admin",
                    "password": "secret"
                }
            },
            "features": ["logging", "caching", "monitoring"],
            "enabled": true
        }
        """;

        // Act
        var result = _handler.Parse(complexJson);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.True(result.ContainsKey("database"));
        Assert.True(result.ContainsKey("features"));
        Assert.True(result.ContainsKey("enabled"));
    }

    [Fact]
    public void ConfigurationTypeHandler_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        var testConfiguration = new Dictionary<string, object> 
        { 
            { "key1", "value1" }, 
            { "key2", "value2" },
            { "port", 8080 },
            { "enabled", true },
            { "timeout", 30.5 }
        };

        // Act
        var parameter = new SqliteParameter();
        _handler.SetValue(parameter, testConfiguration);
        var serialized = parameter.Value?.ToString();
        var deserialized = _handler.Parse(serialized);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(testConfiguration.Count, deserialized.Count);
        Assert.Equal("value1", deserialized["key1"].ToString());
        Assert.Equal("value2", deserialized["key2"].ToString());
        Assert.Equal(8080, Convert.ToInt32(deserialized["port"]));
        Assert.True(Convert.ToBoolean(deserialized["enabled"]));
        Assert.Equal(30.5, Convert.ToDouble(deserialized["timeout"]));
    }

    [Fact]
    public void ConfigurationTypeHandler_ShouldHandleNestedObjects()
    {
        // Arrange
        var nestedConfig = new Dictionary<string, object>
        {
            { "database", new Dictionary<string, object> { { "host", "localhost" }, { "port", 3306 } } },
            { "cache", new Dictionary<string, object> { { "enabled", true }, { "ttl", 3600 } } },
            { "features", new List<object> { "auth", "logging", "monitoring" } }
        };

        // Act
        var parameter = new SqliteParameter();
        _handler.SetValue(parameter, nestedConfig);
        var serialized = parameter.Value?.ToString();
        var deserialized = _handler.Parse(serialized);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.Count);
        
        // Verify nested dictionary
        var database = deserialized["database"] as Dictionary<string, object>;
        Assert.NotNull(database);
        Assert.Equal("localhost", database["host"].ToString());
        Assert.Equal(3306, Convert.ToInt32(database["port"]));
        
        // Verify nested dictionary
        var cache = deserialized["cache"] as Dictionary<string, object>;
        Assert.NotNull(cache);
        Assert.True(Convert.ToBoolean(cache["enabled"]));
        Assert.Equal(3600, Convert.ToInt32(cache["ttl"]));
        
        // Verify nested list
        var features = deserialized["features"] as List<object>;
        Assert.NotNull(features);
        Assert.Equal(3, features.Count);
        Assert.Contains("auth", features);
        Assert.Contains("logging", features);
        Assert.Contains("monitoring", features);
    }

    [Fact]
    public void ConfigurationTypeHandler_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var specialConfig = new Dictionary<string, object>
        {
            { "path", "/usr/local/bin" },
            { "regex", @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$" },
            { "json", "{\"key\": \"value\", \"number\": 123}" },
            { "unicode", "测试中文" },
            { "quotes", "He said \"Hello World\"" }
        };

        // Act
        var parameter = new SqliteParameter();
        _handler.SetValue(parameter, specialConfig);
        var serialized = parameter.Value?.ToString();
        var deserialized = _handler.Parse(serialized);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(5, deserialized.Count);
        Assert.Equal("/usr/local/bin", deserialized["path"].ToString());
        Assert.Equal(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", deserialized["regex"].ToString());
        Assert.Equal("{\"key\": \"value\", \"number\": 123}", deserialized["json"].ToString());
        Assert.Equal("测试中文", deserialized["unicode"].ToString());
        Assert.Equal("He said \"Hello World\"", deserialized["quotes"].ToString());
    }
}
