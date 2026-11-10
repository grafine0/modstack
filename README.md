# ModStack - Modular Web Development Stack Manager

A Windows desktop application for managing modular web development stacks with a modern service-based architecture.

## Features

- **Service Management**: Install, configure, and manage multiple versions of web services
- **Stack Creation**: Create custom stacks grouping different services
- **Project Binding**: Bind projects to specific stacks with virtual host support
- **Configuration Management**: GUI and file-based configuration editing
- **Multi-Stack Support**: Run multiple stacks simultaneously on different ports
- **Windows Services Integration**: Each service runs as an independent Windows Service

## Architecture

The application follows a modular architecture with clear separation of concerns:

- **UI Layer**: WPF GUI with modern interface
- **Core Layer**: Service Manager, Stack Manager, Project Manager, Configuration Manager
- **Data Layer**: SQLite database and file system storage
- **Services Layer**: Windows Services for Apache, Nginx, PHP, MySQL, etc.

## Project Structure

```
ModStack/
├── ModStack.Core/           # Core business logic and models
├── ModStack.Data/           # Data access layer and database
├── ModStack.Services/       # Service management and Windows Services
├── ModStack.GUI/           # WPF desktop application
├── ModStack.Common/        # Shared utilities and constants
└── ModStack.Tests/         # Unit and integration tests
```

## Getting Started

1. Clone the repository
2. Build the solution in Visual Studio 2022
3. Run ModStack.GUI as administrator (required for Windows Service management)
4. Install your first stack using the GUI

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- Administrator privileges (for service management)
- Visual C++ Redistributable (for some services)

## License

MIT License - see LICENSE file for details
