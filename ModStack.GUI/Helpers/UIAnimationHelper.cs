using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ModStack.Logging;

namespace ModStack.GUI.Helpers;

/// <summary>
/// Helper class for UI animations, particularly highlighting animations
/// </summary>
public static class UIAnimationHelper
{
    /// <summary>
    /// Highlights a button with a color animation that fades back to the original background
    /// </summary>
    /// <param name="button">The button to highlight</param>
    /// <param name="duration">Animation duration in seconds (default: 1.5)</param>
    public static void HighlightButton(Button button, double duration = 1.5)
    {
        if (button == null) return;

        try
        {
            // Store original background
            var originalBackground = button.Background;

            // Get theme-aware highlight color
            var highlightColor = GetThemeAwareHighlightColor(button);

            // Create a temporary SolidColorBrush for animation
            var brush = new SolidColorBrush(highlightColor);
            button.Background = brush;

            // Create highlight animation
            var highlightAnimation = new ColorAnimation
            {
                From = highlightColor,
                To = originalBackground is SolidColorBrush originalBrush
                    ? originalBrush.Color
                    : Colors.Transparent,
                Duration = TimeSpan.FromSeconds(duration)
            };

            // Apply animation to background
            Storyboard.SetTarget(highlightAnimation, brush);
            Storyboard.SetTargetProperty(highlightAnimation, new PropertyPath(SolidColorBrush.ColorProperty));

            var storyboard = new Storyboard();
            storyboard.Children.Add(highlightAnimation);

            // Restore original background after animation
            storyboard.Completed += (s, e) =>
            {
                if (originalBackground != null)
                {
                    button.Background = originalBackground;
                }
            };

            storyboard.Begin();
        }
        catch (Exception ex)
        {
            LogService.Error($"Error highlighting button: {ex.Message}");
        }
    }

    /// <summary>
    /// Highlights a FrameworkElement (like Border) with a color animation that fades back to the original background
    /// </summary>
    /// <param name="element">The element to highlight</param>
    /// <param name="duration">Animation duration in seconds (default: 2.0)</param>
    public static void HighlightElement(FrameworkElement element, double duration = 2.0)
    {
        if (element == null) return;

        try
        {
            // Find the border element if it's a container
            Border? border = null;
            if (element is Border b)
            {
                border = b;
            }
            else
            {
                border = FindVisualChild<Border>(element);
            }

            if (border == null) return;

            // Store original background
            var originalBackground = border.Background;

            // Get theme-aware highlight color
            var highlightColor = GetThemeAwareHighlightColor(element);

            // Create highlight animation
            var highlightAnimation = new ColorAnimation
            {
                From = highlightColor,
                To = originalBackground is SolidColorBrush brush ? brush.Color : Colors.Transparent,
                Duration = TimeSpan.FromSeconds(duration)
            };

            // Apply animation to background
            Storyboard.SetTarget(highlightAnimation, border);
            Storyboard.SetTargetProperty(highlightAnimation, new PropertyPath("(Border.Background).(SolidColorBrush.Color)"));

            var storyboard = new Storyboard();
            storyboard.Children.Add(highlightAnimation);
            storyboard.Begin();
        }
        catch (Exception ex)
        {
            LogService.Error($"Error highlighting element: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a theme-aware highlight color based on the current theme
    /// </summary>
    /// <param name="element">The element to get resources from (for theme detection)</param>
    /// <returns>A highlight color appropriate for the current theme</returns>
    public static Color GetThemeAwareHighlightColor(FrameworkElement element)
    {
        try
        {
            // Try to get the current theme's accent color
            var accentBrush = element.TryFindResource("AccentBrush") as SolidColorBrush;
            if (accentBrush != null)
            {
                return accentBrush.Color;
            }

            // Fallback to a theme-aware color
            var isDarkTheme = IsDarkTheme(element);
            return isDarkTheme ? Colors.LightBlue : Colors.LightCyan;
        }
        catch
        {
            // Ultimate fallback
            return Colors.LightBlue;
        }
    }

    /// <summary>
    /// Determines if the current theme is a dark theme
    /// </summary>
    /// <param name="element">The element to get resources from (for theme detection)</param>
    /// <returns>True if dark theme, false otherwise</returns>
    public static bool IsDarkTheme(FrameworkElement element)
    {
        try
        {
            // Check if we're using dark theme by looking at background color
            var backgroundBrush = element.TryFindResource("BackgroundBrush") as SolidColorBrush;
            if (backgroundBrush != null)
            {
                var brightness = (backgroundBrush.Color.R + backgroundBrush.Color.G + backgroundBrush.Color.B) / 3.0;
                return brightness < 128; // Dark theme if average brightness is low
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Finds a visual child of the specified type in the visual tree
    /// </summary>
    /// <typeparam name="T">The type of child to find</typeparam>
    /// <param name="parent">The parent element to search in</param>
    /// <returns>The found child element or null if not found</returns>
    public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }
}

