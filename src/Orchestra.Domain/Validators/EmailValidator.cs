using System.Net.Mail;

namespace Orchestra.Domain.Validators;

public static class EmailValidator
{
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        
        if (email.Length > 255)
            return false;
        
        // Check for exactly one '@'
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0) // No @ or @ at start
            return false;
        
        if (email.LastIndexOf('@') != atIndex) // Multiple @
            return false;
        
        var localPart = email.Substring(0, atIndex);
        var domainPart = email.Substring(atIndex + 1);
        
        if (string.IsNullOrWhiteSpace(localPart) || string.IsNullOrWhiteSpace(domainPart))
            return false;
        
        // Check domain has at least one dot
        var dotIndex = domainPart.IndexOf('.');
        if (dotIndex <= 0 || dotIndex == domainPart.Length - 1)
            return false;
        
        return true;
    }
}