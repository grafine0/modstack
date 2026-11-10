using System;

namespace ModStack.Common.Models;

public class InterceptedEmail
{
    public long Id { get; set; }
    
    public string MessageId { get; set; } = string.Empty;
    
    public string Sender { get; set; } = string.Empty;
    
    public string Recipient { get; set; } = string.Empty;
    
    public string Subject { get; set; } = string.Empty;
    
    public string BodyHtml { get; set; } = string.Empty;
    
    public string BodyText { get; set; } = string.Empty;
    
    public string RawHeaders { get; set; } = string.Empty;
    
    public DateTime ReceivedAt { get; set; }
    
    public bool IsRead { get; set; }
}
