using System.Security.Cryptography;
using System.Text;

namespace VitalFace.Core.Models;

/// <summary>
/// GDPR touch-consent proof. The raw p5.js signature canvas is hashed client-side (or immediately
/// on receipt) and only the hash is persisted — the signature image itself is never stored, so the
/// record cannot be replayed as a biometric artifact.
/// </summary>
public sealed record ConsentRecord
{
    public required string SignatureHash { get; init; }
    public required DateTimeOffset ConsentedAtUtc { get; init; }

    public static ConsentRecord FromSignatureBytes(ReadOnlySpan<byte> signaturePngBytes, DateTimeOffset consentedAtUtc)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(signaturePngBytes, hash);
        return new ConsentRecord
        {
            SignatureHash = Convert.ToHexStringLower(hash),
            ConsentedAtUtc = consentedAtUtc
        };
    }
}
