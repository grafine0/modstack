using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ModStack.Common;

namespace ModStack.GUI.Views;

/// <summary>
/// Splash screen window shown during application startup
/// </summary>
public partial class SplashWindow : Window, INotifyPropertyChanged
{
    private readonly DispatcherTimer _progressTimer;
    private double _progressWidth = 0;
    private readonly double _maxProgressWidth = 200;
    private Storyboard? _dotsStoryboard;
    private bool _manualProgressControl = false;

    public SplashWindow()
    {
        InitializeComponent();
        DataContext = this;
        
        // Initialize progress timer
        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _progressTimer.Tick += ProgressTimer_Tick;
        
        // Start animations immediately after initialization
        Loaded += SplashWindow_Loaded;
        ContentRendered += SplashWindow_ContentRendered;
        
        // Also start animations as soon as possible
        Dispatcher.BeginInvoke(new Action(() =>
        {
            StartAnimations();
        }), DispatcherPriority.Loaded);
        
        // LogService.Info("SplashWindow initialized"); // Commented out since LogService may not be available yet
    }

    private void StartAnimations()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("StartAnimations called");
            
            // Start progress animation
            if (!_progressTimer.IsEnabled)
            {
                _progressTimer.Start();
                System.Diagnostics.Debug.WriteLine("Progress timer started");
            }
            
            // Try storyboard approach first
            try
            {
                if (_dotsStoryboard == null)
                {
                    _dotsStoryboard = (Storyboard)FindResource("DotsAnimation");
                    System.Diagnostics.Debug.WriteLine($"Storyboard found: {_dotsStoryboard != null}");
                }
                
                if (_dotsStoryboard != null)
                {
                    _dotsStoryboard.Begin();
                    System.Diagnostics.Debug.WriteLine("Dots storyboard started");
                }
            }
            catch (Exception storyboardEx)
            {
                System.Diagnostics.Debug.WriteLine($"Storyboard failed: {storyboardEx.Message}");
                
                // Fallback to simple animation
                StartSimpleDotsAnimation();
            }
            
            System.Diagnostics.Debug.WriteLine("SplashWindow animations started successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start splash animations: {ex.Message}");
        }
    }

    private void StartSimpleDotsAnimation()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Starting simple dots animation fallback");
            
            // Simple pulsing animation for all dots
            var pulseAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 1.3,
                Duration = TimeSpan.FromMilliseconds(800),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            
            // Apply to all dots with slight delays
            if (Dot1Scale != null)
            {
                Dot1Scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnimation);
                Dot1Scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnimation);
            }
            
            // Delayed animation for second dot
            var delayedAnimation1 = pulseAnimation.Clone();
            delayedAnimation1.BeginTime = TimeSpan.FromMilliseconds(200);
            if (Dot2Scale != null)
            {
                Dot2Scale.BeginAnimation(ScaleTransform.ScaleXProperty, delayedAnimation1);
                Dot2Scale.BeginAnimation(ScaleTransform.ScaleYProperty, delayedAnimation1);
            }
            
            // More delayed animation for third dot
            var delayedAnimation2 = pulseAnimation.Clone();
            delayedAnimation2.BeginTime = TimeSpan.FromMilliseconds(400);
            if (Dot3Scale != null)
            {
                Dot3Scale.BeginAnimation(ScaleTransform.ScaleXProperty, delayedAnimation2);
                Dot3Scale.BeginAnimation(ScaleTransform.ScaleYProperty, delayedAnimation2);
            }
            
            System.Diagnostics.Debug.WriteLine("Simple dots animation started");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Simple animation also failed: {ex.Message}");
        }
    }

    private void SplashWindow_ContentRendered(object? sender, EventArgs e)
    {
        // Start animations as soon as content is rendered (earlier than Loaded)
        System.Diagnostics.Debug.WriteLine("ContentRendered event fired");
        StartAnimations();
    }

    private void SplashWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Ensure animations are started (backup in case earlier events didn't work)
        System.Diagnostics.Debug.WriteLine("Loaded event fired");
        StartAnimations();
    }

    #region Properties

    public double ProgressWidth
    {
        get => _progressWidth;
        set
        {
            _progressWidth = value;
            OnPropertyChanged(nameof(ProgressWidth));
        }
    }

    #endregion

    #region Animation Methods


    private void ProgressTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            // Only auto-increment if not under manual control
            if (!_manualProgressControl && _progressWidth < _maxProgressWidth)
            {
                _progressWidth += 2; // Increase progress by 2 pixels every 50ms
                
                // Update progress bar width directly
                if (ProgressBar != null)
                {
                    ProgressBar.Width = _progressWidth;
                }
                
                // Also update the property for any bindings
                ProgressWidth = _progressWidth;
            }
            else if (_progressWidth >= _maxProgressWidth)
            {
                // Progress complete - stop auto-increment
                _progressTimer.Stop();
                System.Diagnostics.Debug.WriteLine("Progress animation completed");
            }
        }
        catch (Exception ex)
        {
            // If progress update fails, just continue
            System.Diagnostics.Debug.WriteLine($"Progress update error: {ex.Message}");
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Update the loading text
    /// </summary>
    public void UpdateLoadingText(string text)
    {
        // Find the loading text block and update it
        var loadingTextBlock = FindName("LoadingTextBlock") as TextBlock;
        if (loadingTextBlock != null)
        {
            loadingTextBlock.Text = text;
        }
    }

    /// <summary>
    /// Set progress percentage (0-100)
    /// </summary>
    public void SetProgress(int percentage)
    {
        try
        {
            // Enable manual progress control
            _manualProgressControl = true;
            
            var newWidth = (_maxProgressWidth * percentage) / 100;
            _progressWidth = newWidth;
            ProgressWidth = newWidth;
            
            // Update progress bar directly
            if (ProgressBar != null)
            {
                ProgressBar.Width = newWidth;
                System.Diagnostics.Debug.WriteLine($"Progress set to {percentage}% (width: {newWidth}px)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting progress: {ex.Message}");
        }
    }

    /// <summary>
    /// Close the splash screen
    /// </summary>
    public void CloseSplash()
    {
        _dotsStoryboard?.Stop();
        _progressTimer?.Stop();
        
        // Fade out animation
        var fadeAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(300)
        };
        
        fadeAnimation.Completed += (s, e) => Close();
        
        BeginAnimation(OpacityProperty, fadeAnimation);
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Cleanup

    protected override void OnClosed(EventArgs e)
    {
        _dotsStoryboard?.Stop();
        _progressTimer?.Stop();
        base.OnClosed(e);
    }

    #endregion
}
