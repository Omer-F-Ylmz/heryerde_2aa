using System.Security.Cryptography;
using System.Text;

namespace HerYerde.Business.Rules;

/// <summary>RFC 6238 TOTP (HMAC-SHA1, 30 sn, 6 hane); doğrulayıcı uygulamaların (Google Authenticator vb.) varsayılanı.</summary>
public static class Totp
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int StepSeconds = 30;

    /// <summary>20 baytlık rastgele anahtar, Base32 (32 karakter).</summary>
    public static string NewSecret() => Base32(RandomNumberGenerator.GetBytes(20));

    public static string Code(string secret, DateTimeOffset moment) => CodeAt(FromBase32(secret), moment.ToUnixTimeSeconds() / StepSeconds);

    /// <summary>Saat kaymasına karşı bir önceki ve bir sonraki adım da kabul edilir.</summary>
    public static bool Verify(string secret, string code, DateTimeOffset moment)
    {
        var key = FromBase32(secret);
        var step = moment.ToUnixTimeSeconds() / StepSeconds;
        var matched = false;
        for (var offset = -1; offset <= 1; offset++)
        {
            matched |= CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(CodeAt(key, step + offset)),
                Encoding.ASCII.GetBytes(code));
        }

        return matched;
    }

    /// <summary>Doğrulayıcı uygulamanın okuduğu adres; QR kodun içeriği.</summary>
    public static string SetupUri(string issuer, string account, string secret)
        => $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";

    private static string CodeAt(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(step & 0xFF);
            step >>= 8;
        }

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("000000");
    }

    private static string Base32(byte[] bytes)
    {
        var output = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return output.ToString();
    }

    private static byte[] FromBase32(string text)
    {
        var output = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var character in text.TrimEnd('=').ToUpperInvariant())
        {
            var index = Alphabet.IndexOf(character);
            if (index < 0)
            {
                continue;
            }

            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. output];
    }
}
