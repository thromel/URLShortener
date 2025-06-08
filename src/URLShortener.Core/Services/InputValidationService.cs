using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace URLShortener.Core.Services
{
    public interface IInputValidationService
    {
        bool IsValidUrl(string url);
        string SanitizeCustomAlias(string? alias);
        bool IsReservedWord(string word);
    }

    public class InputValidationService : IInputValidationService
    {
        private static readonly Regex UrlRegex = new(
            @"^https?:\/\/(([a-zA-Z0-9-]+\.)+[a-zA-Z]{2,}|localhost|(\d{1,3}\.){3}\d{1,3})(:\d+)?(\/[^?#]*)?(\?[^#]*)?(#.*)?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(100));

        private static readonly Regex AliasRegex = new(
            @"^[a-zA-Z0-9-]+$",
            RegexOptions.Compiled);

        private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "api", "admin", "login", "logout", "register", "dashboard",
            "settings", "profile", "help", "about", "contact", "terms",
            "privacy", "redirect", "url", "urls", "short", "health",
            "metrics", "analytics", "stats", "status", "config", "system",
            "user", "users", "account", "home", "index", "public", "assets",
            "static", "css", "js", "images", "img", "fonts", "media"
        };

        public bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (url.Length > 2048)
                return false;

            try
            {
                var uri = new Uri(url);
                
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                    return false;

                if (IsPrivateIpAddress(uri.Host))
                    return false;

                return UrlRegex.IsMatch(url);
            }
            catch
            {
                return false;
            }
        }

        public string SanitizeCustomAlias(string? alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
                throw new ArgumentException("Custom alias cannot be empty");

            alias = alias.Trim().ToLowerInvariant();

            if (alias.Length < 3)
                throw new ArgumentException("Custom alias must be at least 3 characters long");

            if (alias.Length > 50)
                throw new ArgumentException("Custom alias must not exceed 50 characters");

            if (!AliasRegex.IsMatch(alias))
                throw new ArgumentException("Custom alias can only contain letters, numbers, and hyphens");

            if (alias.StartsWith('-') || alias.EndsWith('-'))
                throw new ArgumentException("Custom alias cannot start or end with a hyphen");

            if (alias.Contains("--"))
                throw new ArgumentException("Custom alias cannot contain consecutive hyphens");

            if (IsReservedWord(alias))
                throw new ArgumentException($"'{alias}' is a reserved word and cannot be used as a custom alias");

            return alias;
        }

        public bool IsReservedWord(string word)
        {
            return ReservedWords.Contains(word);
        }

        private bool IsPrivateIpAddress(string host)
        {
            if (System.Net.IPAddress.TryParse(host, out var ipAddress))
            {
                byte[] bytes = ipAddress.GetAddressBytes();
                
                // 10.0.0.0/8
                if (bytes[0] == 10)
                    return true;
                
                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    return true;
                
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168)
                    return true;
                
                // 127.0.0.0/8 (loopback)
                if (bytes[0] == 127)
                    return true;
                
                // 169.254.0.0/16 (link-local)
                if (bytes[0] == 169 && bytes[1] == 254)
                    return true;
            }
            
            return false;
        }
    }
}