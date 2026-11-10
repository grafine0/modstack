-- ModStack Database Schema
-- SQLite database for storing configurations, stacks, and projects

-- Service definitions table
CREATE TABLE Services (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    Description TEXT,
    ServiceType TEXT NOT NULL, -- Apache, Nginx, PHP, MySQL, MariaDB, PostgreSQL, MongoDB, Redis
    Version TEXT NOT NULL,
    BinaryPath TEXT NOT NULL,
    ConfigPath TEXT,
    DefaultPort INTEGER,
    IsInstalled BOOLEAN DEFAULT FALSE,
    IsEnabled BOOLEAN DEFAULT TRUE,
    InstallDate DATETIME,
    AutoStart BOOLEAN DEFAULT FALSE,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(Name, Version)
);

-- Stack definitions table
CREATE TABLE Stacks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    DisplayName TEXT NOT NULL,
    Description TEXT,
    IsActive BOOLEAN DEFAULT FALSE,
    IsDefault BOOLEAN DEFAULT FALSE,
    Configuration TEXT DEFAULT '{}',
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Services assigned to stacks
CREATE TABLE StackServices (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    StackId INTEGER NOT NULL,
    ServiceId INTEGER NOT NULL,
    Port INTEGER,
    Priority INTEGER DEFAULT 0, -- Start order priority
    IsEnabled BOOLEAN DEFAULT TRUE,
    Configuration TEXT, -- JSON configuration overrides
    FOREIGN KEY (StackId) REFERENCES Stacks(Id) ON DELETE CASCADE,
    FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE,
    UNIQUE(StackId, ServiceId)
);

-- Projects table
CREATE TABLE Projects (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    DisplayName TEXT NOT NULL,
    Path TEXT NOT NULL,
    Domain TEXT, -- e.g., myproject.local
    StackId INTEGER,
    DocumentRoot TEXT,
    IsActive BOOLEAN DEFAULT TRUE,
    EnableSSL BOOLEAN DEFAULT FALSE,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    Configuration TEXT,
    FOREIGN KEY (StackId) REFERENCES Stacks(Id) ON DELETE SET NULL
);

-- Configuration files table
CREATE TABLE ConfigurationFiles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ServiceId INTEGER NOT NULL,
    FileName TEXT NOT NULL,
    FilePath TEXT NOT NULL,
    FileType TEXT NOT NULL, -- conf, ini, json, yaml
    IsTemplate BOOLEAN DEFAULT FALSE,
    Content TEXT,
    ChecksumMD5 TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE
);

-- Service logs table
CREATE TABLE ServiceLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ServiceId INTEGER NOT NULL,
    LogLevel TEXT NOT NULL, -- DEBUG, INFO, WARNING, ERROR, FATAL
    Message TEXT NOT NULL,
    Details TEXT, -- Stack trace, additional context
    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE
);

-- Application settings table
CREATE TABLE Settings (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Category TEXT NOT NULL,
    Key TEXT NOT NULL,
    Value TEXT,
    DataType TEXT DEFAULT 'string', -- string, int, bool, json
    Description TEXT,
    IsUserSetting BOOLEAN DEFAULT TRUE,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(Category, Key)
);

-- Port allocation table
CREATE TABLE PortAllocations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Port INTEGER NOT NULL UNIQUE,
    ServiceId INTEGER,
    StackId INTEGER,
    Protocol TEXT DEFAULT 'TCP', -- TCP, UDP
    IsReserved BOOLEAN DEFAULT FALSE,
    Description TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE SET NULL,
    FOREIGN KEY (StackId) REFERENCES Stacks(Id) ON DELETE SET NULL
);

-- Packages table for available packages
CREATE TABLE Packages (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    DisplayName TEXT NOT NULL,
    Description TEXT,
    Category TEXT NOT NULL, -- Web Server, Database, Runtime, Cache, etc.
    Icon TEXT, -- Icon path or emoji
    IsPopular BOOLEAN DEFAULT FALSE,
    DownloadSize INTEGER DEFAULT 0, -- Size in bytes
    Tags TEXT, -- JSON array of tags
    Homepage TEXT,
    Documentation TEXT,
    License TEXT,
    LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP,
    IsUserAdded BOOLEAN DEFAULT FALSE, -- True if manually added by user
    PackageType INTEGER DEFAULT 1, -- 1=Service, 2=StandaloneWebApp, 3=Framework, 4=Tool
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Package versions table
CREATE TABLE PackageVersions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PackageId INTEGER NOT NULL,
    Version TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    ServiceName TEXT, -- Auto-generated service name for installation
    ReleaseDate DATETIME,
    IsStable BOOLEAN DEFAULT TRUE,
    IsRecommended BOOLEAN DEFAULT FALSE,
    IsLTS BOOLEAN DEFAULT FALSE,
    Size INTEGER DEFAULT 0, -- Size in bytes
    DownloadUrls TEXT, -- JSON array of download URLs
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (PackageId) REFERENCES Packages(Id) ON DELETE CASCADE,
    UNIQUE(PackageId, Version)
);

-- Package configurations table
CREATE TABLE PackageConfigurations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PackageId INTEGER NOT NULL,
    Port INTEGER DEFAULT 0,
    InstallPath TEXT,
    AutoStart BOOLEAN DEFAULT FALSE,
    RegisterAsService BOOLEAN DEFAULT FALSE,
    Configuration TEXT, -- JSON configuration
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (PackageId) REFERENCES Packages(Id) ON DELETE CASCADE
);

-- Project-Service bindings table (for unbound projects)
CREATE TABLE ProjectServices (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProjectId INTEGER NOT NULL,
    ServiceId INTEGER NOT NULL,
    IsEnabled BOOLEAN DEFAULT TRUE,
    Configuration TEXT, -- JSON configuration overrides
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
    FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE,
    UNIQUE(ProjectId, ServiceId)
);

-- Virtual hosts table
CREATE TABLE VirtualHosts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProjectId INTEGER, -- Optional project association
    ServiceId INTEGER NOT NULL, -- Apache or Nginx service
    ServerName TEXT NOT NULL,
    ServerAlias TEXT,
    DocumentRoot TEXT NOT NULL,
    Port INTEGER DEFAULT 80,
    IsSSL BOOLEAN DEFAULT FALSE,
    SSLCertPath TEXT,
    SSLKeyPath TEXT,
    IsEnabled BOOLEAN DEFAULT TRUE,
    Configuration TEXT, -- Additional virtual host configuration
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
    FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE,
    UNIQUE(ServerName, Port)
);

-- Create indexes for better performance
CREATE INDEX idx_services_type_version ON Services(ServiceType, Version);
CREATE INDEX idx_stacks_active ON Stacks(IsActive);
CREATE INDEX idx_stack_services_stack ON StackServices(StackId);
CREATE INDEX idx_project_services_project ON ProjectServices(ProjectId);
CREATE INDEX idx_project_services_service ON ProjectServices(ServiceId);
CREATE INDEX idx_projects_stack ON Projects(StackId);
CREATE INDEX idx_projects_domain ON Projects(Domain);
CREATE INDEX idx_service_logs_service_timestamp ON ServiceLogs(ServiceId, Timestamp);
CREATE INDEX idx_settings_category_key ON Settings(Category, Key);
CREATE INDEX idx_virtual_hosts_project ON VirtualHosts(ProjectId);
CREATE INDEX idx_virtual_hosts_service ON VirtualHosts(ServiceId);

-- Apache Modules table
CREATE TABLE ApacheModules (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    ModuleName TEXT NOT NULL UNIQUE, -- e.g., ssl_module
    FileName TEXT NOT NULL, -- e.g., modules/mod_ssl.so
    Description TEXT,
    Category TEXT,
    IsEnabled BOOLEAN DEFAULT FALSE,
    IsRequired BOOLEAN DEFAULT FALSE,
    IsAvailable BOOLEAN DEFAULT TRUE,
    ApacheVersion TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_apache_modules_category ON ApacheModules(Category);
CREATE INDEX idx_apache_modules_enabled ON ApacheModules(IsEnabled);
CREATE INDEX idx_apache_modules_version ON ApacheModules(ApacheVersion);

CREATE INDEX idx_packages_category ON Packages(Category);
CREATE INDEX idx_packages_popular ON Packages(IsPopular);
CREATE INDEX idx_packages_user_added ON Packages(IsUserAdded);
CREATE INDEX idx_package_versions_package ON PackageVersions(PackageId);
CREATE INDEX idx_package_versions_stable ON PackageVersions(IsStable);
CREATE INDEX idx_package_configurations_package ON PackageConfigurations(PackageId);

-- Insert default settings
INSERT INTO Settings (Category, Key, Value, DataType, Description, IsUserSetting) VALUES
('Application', 'DataDirectory', '%APPDATA%\ModStack\Data', 'string', 'Application data directory', TRUE),
('Application', 'ServicesDirectory', '%APPDATA%\ModStack\Services', 'string', 'Services installation directory', TRUE),
('Application', 'LogsDirectory', '%APPDATA%\ModStack\Logs', 'string', 'Logs directory', TRUE),
('Application', 'AutoStartServices', 'true', 'bool', 'Auto-start services with Windows', TRUE),
('Application', 'CheckForUpdates', 'true', 'bool', 'Check for application updates', TRUE),
('Network', 'DefaultHttpPort', '80', 'int', 'Default HTTP port', TRUE),
('Network', 'DefaultHttpsPort', '443', 'int', 'Default HTTPS port', TRUE),
('Network', 'PortRangeStart', '8080', 'int', 'Start of available port range', TRUE),
('Network', 'PortRangeEnd', '8999', 'int', 'End of available port range', TRUE),
('Services', 'ServiceStartTimeout', '30', 'int', 'Service start timeout in seconds', TRUE),
('Services', 'ServiceStopTimeout', '30', 'int', 'Service stop timeout in seconds', TRUE),
('Logging', 'LogLevel', 'INFO', 'string', 'Application log level', TRUE),
('Logging', 'MaxLogFileSize', '10485760', 'int', 'Maximum log file size in bytes', TRUE),
('Logging', 'LogRetentionDays', '30', 'int', 'Log retention period in days', TRUE);

-- Create triggers for updating timestamps
CREATE TRIGGER update_services_timestamp 
    AFTER UPDATE ON Services
    BEGIN
        UPDATE Services SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
    END;

CREATE TRIGGER update_stacks_timestamp 
    AFTER UPDATE ON Stacks
    BEGIN
        UPDATE Stacks SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
    END;

CREATE TRIGGER update_projects_timestamp 
    AFTER UPDATE ON Projects
    BEGIN
        UPDATE Projects SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
    END;

CREATE TRIGGER update_project_services_timestamp 
    AFTER UPDATE ON ProjectServices
    BEGIN
        UPDATE ProjectServices SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
    END;

CREATE TRIGGER update_configuration_files_timestamp 
    AFTER UPDATE ON ConfigurationFiles
    BEGIN
        UPDATE ConfigurationFiles SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
    END;

CREATE TRIGGER update_settings_timestamp 
    AFTER UPDATE ON Settings
    BEGIN
        UPDATE Settings SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
    END;

CREATE TRIGGER update_virtual_hosts_timestamp 
    AFTER UPDATE ON VirtualHosts
    BEGIN
        UPDATE VirtualHosts SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
    END;

CREATE TRIGGER update_packages_timestamp 
    AFTER UPDATE ON Packages
    BEGIN
        UPDATE Packages SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
    END;

