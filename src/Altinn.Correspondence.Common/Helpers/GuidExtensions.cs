using System.Security.Cryptography;
using System.Text;

namespace Altinn.Correspondence.Common.Helpers;

public static class GuidExtensions
{
    /// <summary>
    /// Creates a name-based UUID version 5 (SHA-1) as per RFC 4122 using the provided namespace and name.
    /// Deterministic for the same (namespace, name) pair across runs.
    /// </summary>
    /// <param name="namespaceId">The namespace UUID.</param>
    /// <param name="name">The name string.</param>
    /// <returns>A deterministic UUIDv5.</returns>
    public static Guid CreateVersion5(this Guid namespaceId, string name)
    {
        if (name == null)
        {
            name = string.Empty;
        }

        // Convert namespace UUID to network order (big-endian) as required by RFC 4122
        var nsBytes = namespaceId.ToByteArray();
        SwapGuidByteOrder(nsBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);

        // Compute SHA-1 over namespace || name
        byte[] hash;
        using (var sha1 = SHA1.Create())
        {
            sha1.TransformBlock(nsBytes, 0, nsBytes.Length, null, 0);
            sha1.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
            hash = sha1.Hash!;
        }

        // Build UUID from first 16 bytes of SHA-1
        var newGuid = new byte[16];
        Array.Copy(hash, 0, newGuid, 0, 16);

        // Set version (5) and variant (RFC 4122)
        newGuid[6] = (byte)((newGuid[6] & 0x0F) | (5 << 4));
        newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

        // Convert back to little-endian for Guid constructor
        SwapGuidByteOrder(newGuid);
        return new Guid(newGuid);
    }

    private static void SwapGuidByteOrder(byte[] guidBytes)
    {
        // Data1 (4 bytes), Data2 (2 bytes), Data3 (2 bytes) need endianness swap
        Array.Reverse(guidBytes, 0, 4);
        Array.Reverse(guidBytes, 4, 2);
        Array.Reverse(guidBytes, 6, 2);
    }
}


