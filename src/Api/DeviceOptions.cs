namespace Api;

using System.Text;
using System.Security.Cryptography;

using static Convert;

public record class DeviceOptions(string Address, string Username, string Password, TimeSpan Timeout)
{
  public DeviceOptions(string address, string username, string password) : this(address, username, password, TimeSpan.FromSeconds(5))
  { }

  public string EncodedUsername()
  {
	return ToBase64String(Encoding.UTF8.GetBytes(ToHexString(Hash(Encoding.UTF8.GetBytes(Username))).ToLower()));
  }

  public string EncodedPassword()
  {
    return Convert.ToBase64String(Encoding.UTF8.GetBytes(Password));
  }

  private static byte[] Hash(byte[] data)
  {
    using var sha1 = SHA1.Create();

    return sha1.ComputeHash(data);
  }
}
