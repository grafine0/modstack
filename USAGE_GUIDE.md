# ModStack Usage Guide

This comprehensive guide covers all aspects of using ModStack, from installation to advanced configuration.

## Table of Contents

1. [Installation](#installation)
2. [Getting Started](#getting-started)
3. [Service Management](#service-management)
4. [Stack Management](#stack-management)
5. [Project Management](#project-management)
6. [Configuration Management](#configuration-management)
7. [CLI Usage](#cli-usage)
8. [GUI Usage](#gui-usage)
9. [Advanced Features](#advanced-features)
10. [Troubleshooting](#troubleshooting)

## Installation

### Prerequisites

- Windows 10/11 (64-bit)
- Administrator privileges
- .NET 8.0 Runtime
- Visual C++ Redistributable 2022

### Installation Steps

1. **Download ModStack** from the releases page
2. **Run the installer** as Administrator
3. **Choose installation directory** (default: `C:\Program Files\ModStack`)
4. **Complete the installation** and launch ModStack

### First Launch

When you first launch ModStack:

1. The application will initialize the database
2. Create default directories in `%APPDATA%\ModStack`
3. Set up default configuration
4. Show the welcome screen

## Getting Started

### Understanding ModStack Concepts

- **Service**: Individual software components (Apache, PHP, MySQL, etc.)
- **Stack**: Collection of services working together
- **Project**: Web application bound to a specific stack
- **Virtual Host**: Domain configuration for accessing projects

### Basic Workflow

1. **Install Services** → **Create Stack** → **Add Services to Stack** → **Create Project** → **Bind Project to Stack**

### Quick Start Example

```bash
# Using CLI
modstack service install apache-2.4.58
modstack service install php-8.3.0
modstack service install mysql-8.0.35

modstack stack create "my-first-stack"
modstack stack add-service 1 --service apache-2.4.58 --port 80
modstack stack add-service 1 --service php-8.3.0 --port 9000
modstack stack add-service 1 --service mysql-8.0.35 --port 3306

modstack project create "my-website" --path "C:\Projects\my-website" --stack 1
modstack stack start 1
```

## Service Management

### Installing Services

#### Via GUI
1. Navigate to **Services** → **Manage Services**
2. Click **Install New Service**
3. Browse to service binary or use auto-detection
4. Configure service settings
5. Click **Install**

#### Via CLI
```bash
# Install from package
modstack service install apache --version 2.4.58 --source package

# Install from binary path
modstack service install php --version 8.3.0 --binary-path "C:\PHP\php-cgi.exe"

# Install with custom configuration
modstack service install mysql --version 8.0.35 --config-file "C:\MySQL\my.ini"
```

### Service Configuration

#### Apache Configuration
```apache
# Main configuration in httpd.conf
ServerRoot "C:/ModStack/Services/Apache/2.4.58"
Listen 80
DocumentRoot "C:/ModStack/Projects"

# Virtual hosts in httpd-vhosts.conf
<VirtualHost *:80>
    ServerName myproject.local
    DocumentRoot "C:/ModStack/Projects/myproject"
</VirtualHost>
```

#### PHP Configuration
```ini
; php.ini settings
memory_limit = 256M
max_execution_time = 300
upload_max_filesize = 32M
post_max_size = 32M

; Extensions
extension=mysqli
extension=curl
extension=gd
```

#### MySQL Configuration
```ini
[mysqld]
port = 3306
datadir = C:/ModStack/Services/MySQL/8.0.35/data
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
```

### Service Operations

```bash
# Start/Stop/Restart services
modstack service start apache-2.4.58
modstack service stop apache-2.4.58
modstack service restart apache-2.4.58

# Check service status
modstack service status apache-2.4.58
modstack service status  # All services

# View service logs
modstack service logs apache-2.4.58 --tail 50
modstack service logs apache-2.4.58 --since "2024-01-01"

# Uninstall service
modstack service uninstall apache-2.4.58
```

## Stack Management

### Creating Stacks

#### Via GUI
1. Navigate to **Stacks** → **Create Stack**
2. Enter stack name and description
3. Select services to include
4. Configure service ports and priorities
5. Save stack configuration

#### Via CLI
```bash
# Create basic stack
modstack stack create "lamp-stack" --display-name "LAMP Development Stack"

# Create stack with services
modstack stack create "lamp-stack" \
  --services apache-2.4.58:80,php-8.3.0:9000,mysql-8.0.35:3306

# Clone existing stack
modstack stack clone 1 "lamp-stack-clone"
```

### Stack Configuration

#### Adding Services to Stack
```bash
# Add service with default port
modstack stack add-service 1 --service apache-2.4.58

# Add service with custom port and priority
modstack stack add-service 1 --service apache-2.4.58 --port 8080 --priority 3

# Add service with configuration overrides
modstack stack add-service 1 --service php-8.3.0 --config memory_limit=512M
```

#### Service Dependencies
```json
{
  "services": [
    {
      "name": "mysql-8.0.35",
      "priority": 1,
      "dependencies": []
    },
    {
      "name": "php-8.3.0",
      "priority": 2,
      "dependencies": ["mysql-8.0.35"]
    },
    {
      "name": "apache-2.4.58",
      "priority": 3,
      "dependencies": ["php-8.3.0", "mysql-8.0.35"]
    }
  ]
}
```

### Stack Operations

```bash
# Start/Stop stacks
modstack stack start 1
modstack stack stop 1
modstack stack restart 1

# Switch between stacks
modstack stack switch 2

# View stack status
modstack stack status 1
modstack stack list

# Validate stack configuration
modstack stack validate 1

# Export/Import stacks
modstack stack export 1 --file "my-stack.json"
modstack stack import --file "my-stack.json"
```

## Project Management

### Creating Projects

#### Via GUI
1. Navigate to **Projects** → **Create Project**
2. Enter project name and path
3. Select associated stack
4. Configure domain name
5. Set up virtual host settings

#### Via CLI
```bash
# Create basic project
modstack project create "my-webapp" --path "C:\Projects\my-webapp"

# Create project with stack binding
modstack project create "my-webapp" \
  --path "C:\Projects\my-webapp" \
  --stack 1 \
  --domain "my-webapp.local"

# Create project with virtual host configuration
modstack project create "my-webapp" \
  --path "C:\Projects\my-webapp" \
  --stack 1 \
  --domain "my-webapp.local" \
  --document-root "C:\Projects\my-webapp\public"
```

### Project Configuration

#### Virtual Host Setup
```apache
# Automatic virtual host generation
<VirtualHost *:80>
    ServerName my-webapp.local
    DocumentRoot "C:/Projects/my-webapp/public"
    
    <Directory "C:/Projects/my-webapp/public">
        AllowOverride All
        Require all granted
    </Directory>
    
    # PHP-FPM integration
    <FilesMatch \.php$>
        SetHandler "proxy:fcgi://127.0.0.1:9000"
    </FilesMatch>
</VirtualHost>
```

#### Hosts File Management
ModStack automatically manages the Windows hosts file:
```
127.0.0.1 my-webapp.local
127.0.0.1 another-project.local
```

### Project Operations

```bash
# List projects
modstack project list
modstack project list --stack 1

# Bind/Unbind projects to stacks
modstack project bind 1 --stack 2
modstack project unbind 1

# Open project in browser
modstack project open 1

# View project status
modstack project status 1
```

## Configuration Management

### Global Settings

#### Via GUI
1. Navigate to **Settings**
2. Modify application settings
3. Configure default ports and paths
4. Set up auto-start preferences

#### Via CLI
```bash
# View all settings
modstack config list

# Get specific setting
modstack config get Application.DataDirectory

# Set configuration value
modstack config set Network.DefaultHttpPort 8080
modstack config set Application.AutoStartServices true
```

### Service-Specific Configuration

#### Configuration Templates
ModStack uses templates for generating service configurations:

```handlebars
# Apache template example
ServerRoot "{{APACHE_ROOT}}"
Listen {{APACHE_PORT}}
DocumentRoot "{{DOCUMENT_ROOT}}"

{{#PHP_ENABLED}}
<FilesMatch \.php$>
    SetHandler "proxy:fcgi://127.0.0.1:{{PHP_PORT}}"
</FilesMatch>
{{/PHP_ENABLED}}
```

#### Configuration Overrides
```bash
# Set service-specific configuration
modstack service config apache-2.4.58 --set ServerName=mydomain.com
modstack service config php-8.3.0 --set memory_limit=512M

# Apply configuration templates
modstack service config apache-2.4.58 --template custom-apache.conf
```

### Port Management

```bash
# List port allocations
modstack port list
modstack port list --available
modstack port list --used

# Check port availability
modstack port check 8080
modstack port check 3306 --protocol tcp

# Reserve/Release ports
modstack port reserve 8080 --service apache-2.4.58
modstack port release 8080
```

## CLI Usage

### Command Structure
```bash
modstack <command> <subcommand> [arguments] [options]
```

### Service Commands
```bash
modstack service list                    # List all services
modstack service install <name>          # Install service
modstack service uninstall <id>          # Uninstall service
modstack service start <id>              # Start service
modstack service stop <id>               # Stop service
modstack service restart <id>            # Restart service
modstack service status [id]             # Get service status
modstack service logs <id>               # View service logs
modstack service config <id>             # Configure service
```

### Stack Commands
```bash
modstack stack list                      # List all stacks
modstack stack create <name>             # Create new stack
modstack stack delete <id>               # Delete stack
modstack stack start <id>                # Start stack
modstack stack stop <id>                 # Stop stack
modstack stack switch <id>               # Switch to stack
modstack stack status [id]               # Get stack status
modstack stack clone <id> <new-name>     # Clone stack
modstack stack add-service <id>          # Add service to stack
modstack stack remove-service <id>       # Remove service from stack
```

### Project Commands
```bash
modstack project list                    # List all projects
modstack project create <name>           # Create new project
modstack project delete <id>             # Delete project
modstack project bind <id>               # Bind project to stack
modstack project unbind <id>             # Unbind project
modstack project open <id>               # Open project in browser
modstack project status <id>             # Get project status
```

### Global Commands
```bash
modstack status                          # Show system status
modstack version                         # Show version information
modstack config list                     # List configuration
modstack config get <key>                # Get configuration value
modstack config set <key> <value>        # Set configuration value
modstack port list                       # List port allocations
modstack port check <port>               # Check port availability
```

## GUI Usage

### Main Dashboard

The dashboard provides:
- **System Overview**: Active services, stacks, and projects count
- **Quick Actions**: Start/stop active stack, create new stack/project
- **Service Status**: Real-time status of all services
- **Recent Activity**: Log of recent operations

### Service Management View

Features:
- **Service List**: All installed services with status indicators
- **Service Details**: Configuration, logs, and actions for selected service
- **Installation Wizard**: Step-by-step service installation
- **Configuration Editor**: GUI for editing service configuration files

### Stack Management View

Capabilities:
- **Stack List**: All stacks with status and service count
- **Stack Designer**: Visual interface for creating and modifying stacks
- **Service Dependencies**: Graphical representation of service relationships
- **Stack Templates**: Pre-configured stack templates

### Project Management View

Functions:
- **Project Browser**: All projects with domain and stack associations
- **Project Wizard**: Guided project creation with template selection
- **Virtual Host Editor**: GUI for configuring virtual hosts
- **Domain Management**: Automatic hosts file management

### System Tray

The system tray icon provides:
- **Quick Status**: Visual indicator of system status
- **Stack Switching**: Right-click menu for quick stack switching
- **Service Control**: Start/stop individual services
- **Show/Hide**: Toggle main window visibility

## Advanced Features

### Service Templates

Create reusable service configurations:

```json
{
  "name": "apache-development",
  "baseService": "apache",
  "configuration": {
    "modules": ["mod_rewrite", "mod_ssl", "mod_headers"],
    "errorReporting": "detailed",
    "logLevel": "debug"
  }
}
```

### Stack Templates

Pre-configured stack setups:

```json
{
  "name": "lamp-development",
  "description": "LAMP stack optimized for development",
  "services": [
    {
      "type": "apache",
      "version": "latest",
      "configuration": {
        "development_mode": true
      }
    },
    {
      "type": "php",
      "version": "8.3",
      "configuration": {
        "display_errors": true,
        "xdebug_enabled": true
      }
    },
    {
      "type": "mysql",
      "version": "8.0",
      "configuration": {
        "general_log": true
      }
    }
  ]
}
```

### Automation Scripts

#### PowerShell Integration
```powershell
# Create and start development environment
$stack = & modstack stack create "dev-env" --return-id
& modstack stack add-service $stack --service apache-2.4.58
& modstack stack add-service $stack --service php-8.3.0
& modstack stack add-service $stack --service mysql-8.0.35
& modstack stack start $stack

# Create project
$project = & modstack project create "my-app" --stack $stack --return-id
& modstack project open $project
```

#### Batch Scripts
```batch
@echo off
echo Starting development environment...
modstack stack start dev-stack
if %errorlevel% equ 0 (
    echo Development environment started successfully
    start http://localhost
) else (
    echo Failed to start development environment
)
```

### API Integration

ModStack provides a REST API for programmatic access:

```csharp
// C# API client example
var client = new ModStackApiClient("http://localhost:5000");

// Start a stack
await client.Stacks.StartAsync(stackId);

// Get service status
var status = await client.Services.GetStatusAsync(serviceId);

// Create project
var project = await client.Projects.CreateAsync(new CreateProjectRequest
{
    Name = "my-project",
    Path = @"C:\Projects\my-project",
    StackId = stackId,
    Domain = "my-project.local"
});
```

### Configuration Synchronization

Sync configurations across multiple ModStack installations:

```bash
# Export configuration
modstack config export --file "modstack-config.json"

# Import configuration
modstack config import --file "modstack-config.json"

# Sync with remote ModStack instance
modstack sync --remote "http://other-modstack:5000" --direction pull
```

## Troubleshooting

### Common Issues

#### Port Conflicts
```bash
# Check what's using a port
modstack port check 80 --verbose
netstat -ano | findstr :80

# Find alternative ports
modstack port find-available --start 8080 --count 5
```

#### Service Won't Start
```bash
# Check service logs
modstack service logs apache-2.4.58 --tail 100

# Validate configuration
modstack service validate apache-2.4.58

# Test configuration manually
C:\ModStack\Services\Apache\2.4.58\bin\httpd.exe -t
```

#### Permission Issues
```bash
# Run as administrator
# Check file permissions
icacls "C:\ModStack\Services\Apache" /T /Q

# Reset service permissions
modstack service reset-permissions apache-2.4.58
```

### Logging and Diagnostics

#### Application Logs
- Location: `%APPDATA%\ModStack\Logs\`
- Files:
  - `application.log` - Main application log
  - `service-manager.log` - Service management operations
  - `stack-manager.log` - Stack operations
  - `project-manager.log` - Project operations

#### Service Logs
Each service maintains its own logs:
- Apache: `logs/error.log`, `logs/access.log`
- PHP: `logs/php_errors.log`
- MySQL: `logs/error.log`, `logs/slow.log`

#### Debug Mode
```bash
# Enable debug logging
modstack config set Logging.LogLevel Debug

# View detailed logs
modstack logs --level debug --tail 100

# Reset to normal logging
modstack config set Logging.LogLevel Info
```

### Performance Optimization

#### Service Optimization
```bash
# Optimize Apache for development
modstack service config apache-2.4.58 --preset development

# Optimize MySQL for small datasets
modstack service config mysql-8.0.35 --preset small-dataset

# Enable PHP opcache for production
modstack service config php-8.3.0 --enable opcache
```

#### System Resources
```bash
# Monitor resource usage
modstack system monitor

# Adjust service resource limits
modstack service config apache-2.4.58 --set max_workers=150
modstack service config mysql-8.0.35 --set innodb_buffer_pool_size=256M
```

### Getting Help

#### Built-in Help
```bash
modstack --help
modstack service --help
modstack stack create --help
```

#### Log Analysis
```bash
# Search logs for errors
modstack logs --search "error" --since "1 hour ago"

# Export diagnostic information
modstack diagnostic export --file "diagnostic-info.zip"
```

#### Community Support
- GitHub Issues: Report bugs and feature requests
- Documentation: Comprehensive online documentation
- Forums: Community discussion and support

This guide covers the essential aspects of using ModStack. For more detailed information on specific topics, refer to the individual component documentation.
