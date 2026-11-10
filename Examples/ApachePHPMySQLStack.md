# Apache + PHP + MySQL Stack Example

This example demonstrates how to create and configure a traditional LAMP-like stack using ModStack with Apache, PHP, and MySQL services.

## Prerequisites

1. ModStack application installed and running
2. Administrator privileges for Windows Service management
3. Apache HTTP Server binaries
4. PHP binaries and configuration
5. MySQL Server binaries

## Service Definitions

### Apache Service Definition

```json
{
  "name": "apache",
  "displayName": "Apache HTTP Server",
  "serviceType": "Apache",
  "version": "2.4.58",
  "binaryPath": "C:\\ModStack\\Services\\Apache\\2.4.58\\bin\\httpd.exe",
  "configPath": "C:\\ModStack\\Services\\Apache\\2.4.58\\conf\\httpd.conf",
  "defaultPort": 80,
  "isEnabled": true,
  "configuration": {
    "serverRoot": "C:\\ModStack\\Services\\Apache\\2.4.58",
    "documentRoot": "C:\\ModStack\\Projects",
    "serverName": "localhost",
    "listenPort": 80,
    "modules": [
      "mod_rewrite",
      "mod_ssl",
      "mod_proxy",
      "mod_proxy_fcgi",
      "mod_dir",
      "mod_mime"
    ]
  },
  "dependencies": []
}
```

### PHP Service Definition

```json
{
  "name": "php",
  "displayName": "PHP FastCGI Process Manager",
  "serviceType": "PHP",
  "version": "8.3.0",
  "binaryPath": "C:\\ModStack\\Services\\PHP\\8.3.0\\php-cgi.exe",
  "configPath": "C:\\ModStack\\Services\\PHP\\8.3.0\\php.ini",
  "defaultPort": 9000,
  "isEnabled": true,
  "configuration": {
    "phpIniPath": "C:\\ModStack\\Services\\PHP\\8.3.0\\php.ini",
    "extensionDir": "C:\\ModStack\\Services\\PHP\\8.3.0\\ext",
    "enabledExtensions": [
      "mysqli",
      "pdo_mysql",
      "curl",
      "gd",
      "mbstring",
      "openssl",
      "zip",
      "json"
    ],
    "fastcgiPort": 9000,
    "maxChildren": 10,
    "memoryLimit": "256M",
    "maxExecutionTime": 300
  },
  "dependencies": []
}
```

### MySQL Service Definition

```json
{
  "name": "mysql",
  "displayName": "MySQL Database Server",
  "serviceType": "MySQL",
  "version": "8.0.35",
  "binaryPath": "C:\\ModStack\\Services\\MySQL\\8.0.35\\bin\\mysqld.exe",
  "configPath": "C:\\ModStack\\Services\\MySQL\\8.0.35\\my.ini",
  "defaultPort": 3306,
  "isEnabled": true,
  "configuration": {
    "dataDir": "C:\\ModStack\\Services\\MySQL\\8.0.35\\data",
    "logError": "C:\\ModStack\\Services\\MySQL\\8.0.35\\logs\\error.log",
    "port": 3306,
    "maxConnections": 100,
    "innodbBufferPoolSize": "128M",
    "rootPassword": "generated_secure_password",
    "defaultCharacterSet": "utf8mb4",
    "collationServer": "utf8mb4_unicode_ci"
  },
  "dependencies": []
}
```

## Stack Configuration

### Creating the Stack via CLI

```bash
# Create a new stack
modstack stack create "lamp-basic" --display-name "LAMP Basic Stack" --description "Apache + PHP 8.3 + MySQL 8.0"

# Add services to the stack (assuming service IDs 1, 2, 3)
modstack stack add-service 1 --service-id 3 --port 3306 --priority 1  # MySQL first
modstack stack add-service 1 --service-id 2 --port 9000 --priority 2  # PHP second
modstack stack add-service 1 --service-id 1 --port 80 --priority 3    # Apache last

# Start the stack
modstack stack start 1
```

### Stack JSON Configuration

```json
{
  "id": 1,
  "name": "lamp-basic",
  "displayName": "LAMP Basic Stack",
  "description": "Apache + PHP 8.3 + MySQL 8.0",
  "isActive": true,
  "services": [
    {
      "serviceId": 3,
      "port": 3306,
      "priority": 1,
      "isEnabled": true,
      "configuration": {
        "autoStart": true,
        "restartOnFailure": true
      }
    },
    {
      "serviceId": 2,
      "port": 9000,
      "priority": 2,
      "isEnabled": true,
      "configuration": {
        "autoStart": true,
        "dependsOn": ["mysql"]
      }
    },
    {
      "serviceId": 1,
      "port": 80,
      "priority": 3,
      "isEnabled": true,
      "configuration": {
        "autoStart": true,
        "dependsOn": ["mysql", "php"]
      }
    }
  ]
}
```

## Apache Configuration

### Main httpd.conf Configuration

```apache
# Server Root and Basic Settings
ServerRoot "C:/ModStack/Services/Apache/2.4.58"
Listen 80
ServerName localhost:80

# Load essential modules
LoadModule rewrite_module modules/mod_rewrite.so
LoadModule ssl_module modules/mod_ssl.so
LoadModule proxy_module modules/mod_proxy.so
LoadModule proxy_fcgi_module modules/mod_proxy_fcgi.so
LoadModule dir_module modules/mod_dir.so
LoadModule mime_module modules/mod_mime.so

# Document Root
DocumentRoot "C:/ModStack/Projects"

# Directory Settings
<Directory "C:/ModStack/Projects">
    Options Indexes FollowSymLinks
    AllowOverride All
    Require all granted
</Directory>

# PHP-FPM Integration
<FilesMatch \.php$>
    SetHandler "proxy:fcgi://127.0.0.1:9000"
</FilesMatch>

# Directory Index
DirectoryIndex index.php index.html index.htm

# MIME Types
TypesConfig conf/mime.types

# Include virtual hosts
Include conf/extra/httpd-vhosts.conf

# Error and Access Logs
ErrorLog "logs/error.log"
CustomLog "logs/access.log" common
```

### Virtual Hosts Configuration (httpd-vhosts.conf)

```apache
# Default Virtual Host
<VirtualHost *:80>
    ServerName localhost
    DocumentRoot "C:/ModStack/Projects/default"
    ErrorLog "logs/localhost-error.log"
    CustomLog "logs/localhost-access.log" common
</VirtualHost>

# Project Virtual Host Template
<VirtualHost *:80>
    ServerName {PROJECT_DOMAIN}
    DocumentRoot "C:/ModStack/Projects/{PROJECT_NAME}"
    
    <Directory "C:/ModStack/Projects/{PROJECT_NAME}">
        AllowOverride All
        Require all granted
    </Directory>
    
    # PHP-FPM for this virtual host
    <FilesMatch \.php$>
        SetHandler "proxy:fcgi://127.0.0.1:9000"
    </FilesMatch>
    
    ErrorLog "logs/{PROJECT_NAME}-error.log"
    CustomLog "logs/{PROJECT_NAME}-access.log" common
</VirtualHost>
```

## PHP Configuration

### php.ini Key Settings

```ini
; Basic Settings
engine = On
short_open_tag = Off
precision = 14
output_buffering = 4096
zlib.output_compression = Off
implicit_flush = Off
serialize_precision = -1

; Error Handling
display_errors = On
display_startup_errors = On
log_errors = On
log_errors_max_len = 1024
ignore_repeated_errors = Off
ignore_repeated_source = Off
report_memleaks = On
track_errors = Off
error_log = "C:\ModStack\Services\PHP\8.3.0\logs\php_errors.log"

; Resource Limits
max_execution_time = 300
max_input_time = 60
memory_limit = 256M
post_max_size = 32M
upload_max_filesize = 32M
max_file_uploads = 20

; Extensions
extension_dir = "C:\ModStack\Services\PHP\8.3.0\ext"
extension=mysqli
extension=pdo_mysql
extension=curl
extension=gd
extension=mbstring
extension=openssl
extension=zip
extension=fileinfo

; Session Settings
session.save_handler = files
session.save_path = "C:\ModStack\Services\PHP\8.3.0\tmp"
session.use_cookies = 1
session.use_only_cookies = 1
session.name = PHPSESSID
session.auto_start = 0
session.cookie_lifetime = 0
session.cookie_path = /
session.cookie_domain =
session.cookie_httponly = 1

; Date/Time
date.timezone = "UTC"

; FastCGI Settings
cgi.rfc2616_headers = 1
fastcgi.impersonate = 1
```

## MySQL Configuration

### my.ini Configuration

```ini
[mysqld]
# Basic Settings
port = 3306
basedir = "C:/ModStack/Services/MySQL/8.0.35"
datadir = "C:/ModStack/Services/MySQL/8.0.35/data"
tmpdir = "C:/ModStack/Services/MySQL/8.0.35/tmp"
socket = "C:/ModStack/Services/MySQL/8.0.35/mysql.sock"

# Character Sets
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
init-connect = 'SET NAMES utf8mb4'

# Connection Settings
max_connections = 100
max_connect_errors = 100000
back_log = 100
max_allowed_packet = 64M

# Buffer Settings
key_buffer_size = 32M
myisam_sort_buffer_size = 128M
join_buffer_size = 8M
read_buffer_size = 2M
read_rnd_buffer_size = 8M
sort_buffer_size = 8M
table_open_cache = 4000
thread_cache_size = 286
query_cache_type = 1
query_cache_size = 64M

# InnoDB Settings
innodb_buffer_pool_size = 128M
innodb_log_file_size = 64M
innodb_log_buffer_size = 16M
innodb_flush_log_at_trx_commit = 1
innodb_file_per_table = 1
innodb_open_files = 400

# Logging
log-error = "C:/ModStack/Services/MySQL/8.0.35/logs/error.log"
slow_query_log = 1
slow_query_log_file = "C:/ModStack/Services/MySQL/8.0.35/logs/slow.log"
long_query_time = 2

# Security
sql_mode = STRICT_TRANS_TABLES,NO_ZERO_DATE,NO_ZERO_IN_DATE,ERROR_FOR_DIVISION_BY_ZERO

[mysql]
default-character-set = utf8mb4

[client]
default-character-set = utf8mb4
port = 3306
```

## Example Project Setup

### Creating a Project

```bash
# Create a new project
modstack project create "my-webapp" --path "C:\ModStack\Projects\my-webapp" --domain "my-webapp.local" --stack-id 1

# The system will automatically:
# 1. Create the project directory structure
# 2. Add virtual host configuration to Apache
# 3. Update the Windows hosts file with the domain
# 4. Restart Apache to load the new configuration
```

### Project Structure

```
C:\ModStack\Projects\my-webapp\
├── public\
│   ├── index.php
│   ├── css\
│   ├── js\
│   └── images\
├── config\
│   └── database.php
├── includes\
├── logs\
└── .htaccess
```

### Sample index.php

```php
<?php
// Test PHP and MySQL connectivity
echo "<h1>ModStack LAMP Test</h1>";

// PHP Info
echo "<h2>PHP Information</h2>";
echo "PHP Version: " . phpversion() . "<br>";
echo "Server Software: " . $_SERVER['SERVER_SOFTWARE'] . "<br>";
echo "Document Root: " . $_SERVER['DOCUMENT_ROOT'] . "<br>";

// MySQL Connection Test
echo "<h2>MySQL Connection Test</h2>";
try {
    $pdo = new PDO('mysql:host=localhost:3306;dbname=test', 'root', 'your_password');
    $pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
    echo "<span style='color: green;'>✓ MySQL connection successful</span><br>";
    
    // Get MySQL version
    $stmt = $pdo->query('SELECT VERSION() as version');
    $version = $stmt->fetch(PDO::FETCH_ASSOC);
    echo "MySQL Version: " . $version['version'] . "<br>";
} catch (PDOException $e) {
    echo "<span style='color: red;'>✗ MySQL connection failed: " . $e->getMessage() . "</span><br>";
}

// Loaded Extensions
echo "<h2>Loaded PHP Extensions</h2>";
$extensions = get_loaded_extensions();
sort($extensions);
foreach ($extensions as $extension) {
    echo $extension . "<br>";
}
?>
```

## Starting the Stack

### Using CLI

```bash
# Start the entire stack
modstack stack start 1

# Or start services individually in order
modstack service start 3  # MySQL
modstack service start 2  # PHP
modstack service start 1  # Apache

# Check status
modstack stack status 1
modstack status
```

### Using GUI

1. Open ModStack GUI application
2. Navigate to "Stack Management"
3. Select "LAMP Basic Stack"
4. Click "Start Stack" button
5. Monitor the service startup order and status

## Testing the Setup

1. **Open browser** and navigate to `http://my-webapp.local`
2. **Verify PHP** is working by checking the test page
3. **Test MySQL connection** through the PHP test script
4. **Check logs** in case of any issues:
   - Apache: `C:\ModStack\Services\Apache\2.4.58\logs\`
   - PHP: `C:\ModStack\Services\PHP\8.3.0\logs\`
   - MySQL: `C:\ModStack\Services\MySQL\8.0.35\logs\`

## Troubleshooting

### Common Issues

1. **Port conflicts**: Check if ports 80, 3306, or 9000 are in use
2. **Permission issues**: Ensure ModStack is running as administrator
3. **Path problems**: Verify all service paths exist and are accessible
4. **Service dependencies**: Ensure services start in the correct order

### CLI Debugging Commands

```bash
# Check port usage
modstack port list

# View service logs
modstack service logs 1 --tail 50

# Validate stack configuration
modstack stack validate 1

# Check service status
modstack service status
```

This example provides a complete, working LAMP stack that can be easily deployed and managed through ModStack.
