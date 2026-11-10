namespace ModStack.Common.Enums
{
    /// <summary>
    /// Defines the type of package installation
    /// </summary>
    public enum PackageType
    {
        /// <summary>
        /// System service package (Apache, MySQL, PHP, Nginx)
        /// Installed to /Services folder and registered as Windows services
        /// </summary>
        Service = 1,

        /// <summary>
        /// Standalone web application (phpMyAdmin, Adminer)
        /// Creates its own project and virtual host
        /// </summary>
        StandaloneWebApp = 2,

        /// <summary>
        /// Framework package (Laravel, WordPress, Symfony)
        /// Installed within an existing project
        /// </summary>
        Framework = 3,

        /// <summary>
        /// Development tool package (Composer, Node.js)
        /// Installed to /Tools folder and added to system PATH
        /// </summary>
        Tool = 4
    }
}
