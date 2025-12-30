using System.Security.Cryptography;

namespace Fawkes.Api;

public static class PasswordGenerator
{
	public static string GeneratePassword()
	{
		using var aes = Aes.Create();
		aes.KeySize = 256;
		aes.GenerateKey();
		return Convert.ToBase64String(aes.Key);
	}
}
