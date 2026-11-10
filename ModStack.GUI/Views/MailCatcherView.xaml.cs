using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
using ModStack.Common.Models;
using ModStack.GUI.ViewModels;

namespace ModStack.GUI.Views;

public static class WebBrowserProperties
{
    public static readonly DependencyProperty EmailProperty = DependencyProperty.RegisterAttached(
        "Email", typeof(InterceptedEmail), typeof(WebBrowserProperties), new UIPropertyMetadata(null, OnEmailChanged));

    public static InterceptedEmail GetEmail(DependencyObject obj)
    {
        return (InterceptedEmail)obj.GetValue(EmailProperty);
    }

    public static void SetEmail(DependencyObject obj, InterceptedEmail value)
    {
        obj.SetValue(EmailProperty, value);
    }

    private static async void OnEmailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WebView2 browser)
        {
            var email = e.NewValue as InterceptedEmail;
            
            if (email == null)
            {
                await browser.EnsureCoreWebView2Async(null);
                browser.NavigateToString("<html><body></body></html>");
                return;
            }

            string html = "";
            if (!string.IsNullOrWhiteSpace(email.BodyHtml))
            {
                html = email.BodyHtml;
            }
            else if (!string.IsNullOrWhiteSpace(email.BodyText))
            {
                html = $"<html><body><pre>{System.Net.WebUtility.HtmlEncode(email.BodyText)}</pre></body></html>";
            }
            else
            {
                html = $"<html><body style='font-family: sans-serif; color: gray; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0;'><h2>(This email has no body content)</h2></body></html>";
            }

            try
            {
                // Ensure WebView2 is initialized
                await browser.EnsureCoreWebView2Async(null);
                
                // NavigateToString can silently fail if called during layout passes.
                // The most robust way to force WebView2 to render in WPF is to write to a temp file
                // and bind the Source property, which integrates perfectly with the WPF layout engine.
                var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ModStack_Email_{email.Id}_{Guid.NewGuid():N}.html");
                System.IO.File.WriteAllText(tempFile, "<!DOCTYPE html>\n<html>\n<head><meta charset=\"utf-8\"/></head>\n<body style=\"font-family: sans-serif;\">\n" + html + "\n</body>\n</html>", System.Text.Encoding.UTF8);
                
                browser.Source = new Uri(tempFile);
            }
            catch { }
        }
    }
}

public partial class MailCatcherView : UserControl
{
    private MailCatcherViewModel? _viewModel;

#if DEBUG
    public MailCatcherView()
    {
        InitializeComponent();
    }
#endif

    public MailCatcherView(MailCatcherViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel = viewModel;
    }
}
