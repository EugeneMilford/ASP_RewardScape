using System.Security.Cryptography;

namespace SpendWell.Api.Common;

/// <summary>
/// Random reference codes for orders and bookings. Leaves out 0/O and 1/I so codes
/// read out over the phone can't be confused.
/// </summary>
public static class CodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Random(int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }
}
