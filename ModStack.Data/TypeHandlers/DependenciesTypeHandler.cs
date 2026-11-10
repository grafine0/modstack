using System.Data;
using System.Text.Json;
using Dapper;

namespace ModStack.Data.Repositories;

/// <summary>
/// Custom type handler for Dapper to serialize/deserialize service Dependencies list
/// </summary>
public class DependenciesTypeHandler : SqlMapper.TypeHandler<List<string>>
{
    public override void SetValue(IDbDataParameter parameter, List<string>? value)
    {
        parameter.Value = value is { Count: > 0 }
            ? JsonSerializer.Serialize(value)
            : "[]";
    }

    public override List<string> Parse(object? value)
    {
        if (value == null || value == DBNull.Value)
            return new List<string>();

        var jsonString = value.ToString();
        if (string.IsNullOrWhiteSpace(jsonString))
            return new List<string>();

        try
        {
            using var document = JsonDocument.Parse(jsonString);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new List<string>();

            var result = new List<string>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var str = item.GetString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        result.Add(str);
                    }
                }
            }

            return result;
        }
        catch
        {
            return new List<string>();
        }
    }
}

