# Database Migrations System

This document describes the database migrations system implemented in ModStack, which allows for safe schema evolution without requiring users to reset their databases.

## Overview

The migrations system ensures that:
- **First-time users**: Get a complete database setup with schema, seed data, and all migrations applied
- **Existing users**: Get only new migrations applied, preserving their existing data
- **Schema evolution**: New versions can safely add columns, indexes, and tables without breaking existing installations

## Architecture

### Core Components

1. **`IMigration` Interface**: Defines the contract for all migrations
2. **`MigrationRunner`**: Orchestrates the execution of migrations
3. **Migration Classes**: Individual migration implementations
4. **`__Migrations` Table**: Tracks which migrations have been applied

### Migration Interface

```csharp
public interface IMigration
{
    string Id { get; }          // e.g. "001_AddDescriptionColumn"
    string Description { get; } // human-readable description
    Task ApplyAsync(SqliteConnection connection, ILogger logger);
}
```

### Migration Runner

The `MigrationRunner` class:
- Discovers all available migrations
- Orders them by ID (lexicographically)
- Checks which migrations have already been applied
- Executes only pending migrations
- Records successful migrations in the `__Migrations` table

## Migration Lifecycle

### First Application Launch
1. Database schema is created via `DatabaseInitializer`
2. Seed data is populated (packages and versions)
3. All migrations are applied in order
4. `__Migrations` table is created and populated

### Subsequent Launches
1. Existing database is detected
2. Seed data is skipped (already exists)
3. Only new migrations are applied
4. Diagnostics are logged

## Auto-Discovery of Migrations

The migration system now automatically discovers all migration classes using reflection, eliminating the need to manually register each migration:

```csharp
// Auto-discover all migration classes using reflection
var migrationTypes = typeof(IMigration).Assembly
    .GetTypes()
    .Where(t => typeof(IMigration).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

var migrations = migrationTypes
    .Select(t => (IMigration)Activator.CreateInstance(t)!)
    .OrderBy(m => m.Id)
    .ToList();
```

### Benefits of Auto-Discovery

- **No Manual Registration**: Just drop a new migration class in the Migrations folder
- **Automatic Ordering**: Migrations are automatically ordered by ID
- **Zero Configuration**: No need to update migration lists
- **Easy Maintenance**: Add/remove migrations without touching other code

### Creating New Migrations

Simply create a new class implementing `IMigration`:

```csharp
public class Migration005_AddNewFeature : IMigration
{
    public string Id => "005_AddNewFeature";
    public string Description => "Add new feature to database";

    public async Task ApplyAsync(SqliteConnection connection, ILogger logger)
    {
        // Your migration logic here
    }
}
```

The system will automatically discover and run this migration!

## Strongly Typed Seed Data

The seed data system now uses strongly typed DTOs for better type safety and maintainability:

### Seed Data DTOs

```csharp
public class SeedPackage
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsPopular { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<SeedPackageVersion> Versions { get; set; } = new();
}

public class SeedPackageVersion
{
    public string Version { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public bool IsStable { get; set; }
    public bool IsRecommended { get; set; }
    public bool IsLTS { get; set; }
    public long Size { get; set; }
    public List<string> DownloadUrls { get; set; } = new();
}
```

### Benefits of Strongly Typed Seed Data

- **Type Safety**: Compile-time validation of seed data structure
- **IntelliSense**: Full IDE support for properties and methods
- **Consistency**: Same DTOs used for JSON and fallback data
- **Maintainability**: Easy to refactor and extend seed data structure
- **Validation**: Automatic validation of required fields

### Shared Logic

The seed data system now uses a single, streamlined approach:

- **JSON-First**: Always loads from `DefaultPackages.json` file
- **No Fallback**: If JSON is missing or invalid, the database remains empty (no hardcoded fallback)
- **Simplified Code**: Single method handles all package creation logic
- **Clean Architecture**: No duplicate code paths or complex fallback mechanisms

## Migration Best Practices

### 1. Naming Convention
- Use sequential numbering: `001_`, `002_`, `003_`, etc.
- Use descriptive names: `AddUserPreferences`, `CreateIndexesForPerformance`
- Keep IDs short but meaningful
- **Important**: Always use zero-padded numbers (001, 002, 010) to ensure proper ordering

### 3. Migration Ordering
- Migrations are ordered by parsing the numeric prefix: `int.Parse(m.Id.Split('_')[0])`
- This ensures proper ordering: `001_` → `002_` → `010_` → `100_`
- String ordering would incorrectly place `10_` before `2_`
- Always use zero-padded numbers in migration IDs

### 4. Error Handling
- Catch specific SQLite error codes
- Log appropriate messages for different scenarios
- Don't fail silently - always log what happened

### 5. Testing
- Test migrations on fresh databases
- Test migrations on databases with existing data
- Verify rollback scenarios (if needed)

## Migration Types

### Schema Changes
- Adding columns: `ALTER TABLE table ADD COLUMN column type`
- Creating indexes: `CREATE INDEX IF NOT EXISTS idx_name ON table(column)`
- Creating tables: `CREATE TABLE IF NOT EXISTS table (...)`

### Data Migrations
- Updating existing data: `UPDATE table SET column = value WHERE condition`
- Inserting default values: `INSERT OR IGNORE INTO table (...)`

### Performance Optimizations
- Adding indexes for frequently queried columns
- Creating covering indexes for complex queries
- Analyzing table statistics

## Migration History

The `__Migrations` table tracks applied migrations:

```sql
CREATE TABLE IF NOT EXISTS __Migrations (
    Id TEXT PRIMARY KEY,
    AppliedOn TEXT NOT NULL,
    Description TEXT
);
```

### Querying Migration History

```csharp
// Get list of applied migrations
var appliedMigrations = await migrationRunner.GetAppliedMigrationsAsync(connectionString);

// Check if specific migration was applied
var hasMigration = await HasMigrationAsync(connection, "001_AddDescriptionColumn");
```

## Troubleshooting

### Common Issues

1. **Migration Already Applied**: Handle gracefully with try-catch
2. **Schema Conflicts**: Use `IF NOT EXISTS` clauses
3. **Data Type Mismatches**: Test with existing data
4. **Performance Impact**: Consider running large migrations in batches

### Debugging

Enable detailed logging to see migration progress:

```csharp
_logger.LogInformation("🔄 Starting database migrations...");
_logger.LogInformation("📦 Applying migration {Id}: {Description}", migration.Id, migration.Description);
_logger.LogInformation("✅ Migration {Id} applied successfully", migration.Id);
```

## Future Enhancements

### Potential Improvements

1. **Rollback Support**: Implement down migrations for rollbacks
2. **Migration Dependencies**: Support for migration dependencies
3. **Batch Processing**: Handle large data migrations in chunks
4. **Validation**: Pre-migration validation checks
5. **Backup**: Automatic database backup before migrations

### Integration with CI/CD

Consider adding migration validation to your build process:
- Verify all migrations compile
- Test migrations on sample data
- Validate migration ordering

## Example Migration Scenarios

### Adding a New Column
```csharp
public class Migration005_AddUserPreferences : IMigration
{
    public string Id => "005_AddUserPreferences";
    public string Description => "Add user preferences column to Users table";

    public async Task ApplyAsync(SqliteConnection connection, ILogger logger)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE Users ADD COLUMN Preferences TEXT DEFAULT '{}'";
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("✅ Applied migration {Id}: {Description}", Id, Description);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            logger.LogInformation("ℹ️ Migration {Id} skipped: column already exists", Id);
        }
    }
}
```

### Creating Performance Indexes
```csharp
public class Migration006_AddSearchIndexes : IMigration
{
    public string Id => "006_AddSearchIndexes";
    public string Description => "Add search performance indexes";

    public async Task ApplyAsync(SqliteConnection connection, ILogger logger)
    {
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_projects_name ON Projects(Name)",
            "CREATE INDEX IF NOT EXISTS idx_projects_domain ON Projects(Domain)",
            "CREATE INDEX IF NOT EXISTS idx_projects_created ON Projects(CreatedAt)"
        };

        foreach (var indexSql in indexes)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = indexSql;
                await cmd.ExecuteNonQueryAsync();
                logger.LogDebug("✅ Created index: {IndexSql}", indexSql);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                logger.LogDebug("ℹ️ Index already exists: {IndexSql}", indexSql);
            }
        }

        logger.LogInformation("✅ Applied migration {Id}: {Description}", Id, Description);
    }
}
```

This migrations system provides a robust foundation for database schema evolution while maintaining data integrity and user experience.
