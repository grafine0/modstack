namespace ModStack.Common.Models;

/// <summary>
/// Represents an application setting
/// </summary>
public class AppSetting
{
    public long Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string DataType { get; set; } = "string";
    public string? Description { get; set; }
    public bool IsUserSetting { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Gets the full key (Category.Key)
    /// </summary>
    public string FullKey => $"{Category}.{Key}";
    
    /// <summary>
    /// Gets the typed value of the setting
    /// </summary>
    public T GetValue<T>()
    {
        if (string.IsNullOrEmpty(Value))
            return default(T)!;
        
        var type = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        
        try
        {
            if (underlyingType == typeof(bool))
            {
                return (T)(object)bool.Parse(Value);
            }
            else if (underlyingType == typeof(int))
            {
                return (T)(object)int.Parse(Value);
            }
            else if (underlyingType == typeof(long))
            {
                return (T)(object)long.Parse(Value);
            }
            else if (underlyingType == typeof(double))
            {
                return (T)(object)double.Parse(Value);
            }
            else if (underlyingType == typeof(decimal))
            {
                return (T)(object)decimal.Parse(Value);
            }
            else if (underlyingType == typeof(DateTime))
            {
                return (T)(object)DateTime.Parse(Value);
            }
            else
            {
                return (T)(object)Value;
            }
        }
        catch
        {
            return default(T)!;
        }
    }
    
    /// <summary>
    /// Sets the value from a typed object
    /// </summary>
    public void SetValue<T>(T value)
    {
        if (value == null)
        {
            Value = null;
            return;
        }
        
        Value = value.ToString();
        DataType = GetDataTypeString<T>();
    }
    
    private static string GetDataTypeString<T>()
    {
        var type = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        
        if (underlyingType == typeof(bool))
            return "bool";
        else if (underlyingType == typeof(int) || underlyingType == typeof(long))
            return "int";
        else if (underlyingType == typeof(double) || underlyingType == typeof(decimal))
            return "decimal";
        else if (underlyingType == typeof(DateTime))
            return "datetime";
        else
            return "string";
    }
}
