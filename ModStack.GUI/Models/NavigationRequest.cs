namespace ModStack.GUI.Models
{
    /// <summary>
    /// Defines the available navigation actions in the application
    /// </summary>
    public enum NavigationAction
    {
        /// <summary>
        /// Navigate to create a new project
        /// </summary>
        CreateProject,
        
        /// <summary>
        /// Navigate to edit an existing project
        /// </summary>
        EditProject,
        
        /// <summary>
        /// Navigate to project management view
        /// </summary>
        ProjectManagement,
        
        /// <summary>
        /// Navigate to service management view
        /// </summary>
        ServiceManagement,
        
        /// <summary>
        /// Navigate to stack management view
        /// </summary>
        StackManagement
    }

    /// <summary>
    /// Strongly-typed navigation request with action and optional parameter
    /// </summary>
    /// <param name="Action">The navigation action to perform</param>
    /// <param name="Parameter">Optional parameter for the navigation action (e.g., project ID for EditProject)</param>
    public record NavigationRequest(NavigationAction Action, object? Parameter = null);
}
