using ModStack.Common.Models;

namespace ModStack.Data.Interfaces;

/// <summary>
/// Repository interface for application settings
/// </summary>
public interface ISettingsRepository
{
    Task<AppSetting?> GetByCategoryAndKeyAsync(string category, string key);
    Task<List<AppSetting>> GetByCategoryAsync(string category);
    Task<List<AppSetting>> GetAllAsync();
    Task<AppSetting> CreateAsync(AppSetting setting);
    Task<AppSetting> UpdateAsync(AppSetting setting);
    Task DeleteAsync(long id);
}
