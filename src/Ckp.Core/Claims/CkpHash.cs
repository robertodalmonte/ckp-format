namespace Ckp.Core.Claims;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Content-addressing helper for claim statements. Every hash emitted by or checked
/// against a .ckp package routes through here — there is no separate inline
/// implementation anywhere else in the codebase.
/// </summary>
public static class CkpHash
{
    /// <summary>
    /// Computes the CKP canonical hash of <paramref name="statement"/>:
    /// <c>sha256:</c> prefix followed by the lowercase hex SHA-256 of the UTF-8 bytes.
    /// </summary>
    public static string OfStatement(string statement)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(statement));
        return $"sha256:{Convert.ToHexStringLower(bytes)}";
    }
}
