using System.Security.Cryptography;
using System.Text;

namespace Sensus.Application.Common.Moderation;

public static class ModeratedTextFingerprint
{
    public static string Create(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim())));
}
