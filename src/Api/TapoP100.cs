namespace Api;

using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Net;

using System.Security.Cryptography;
using System.Threading.Tasks;

using Api.Exceptions;

using System.Text.Json.Serialization;

public class TapoP100 : ITapoP100, IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly JsonSerializerOptions _jsonSerializerOptions;

	private readonly string _username;
	private readonly string _password;

	private ICryptoTransform? _encryptor;
	private ICryptoTransform? _decryptor;
	private bool _disposedValue;

	protected Uri BaseUri { get; }
	protected Uri AppTokenUri => new(BaseUri, $"app?token={Token}");

	protected string? Token { get; private set; }

	public TapoP100(DeviceOptions options)
	{
		BaseUri = new Uri($"http://{options.Address}");
		_username = options.EncodedUsername();
		_password = options.EncodedPassword();

		_httpClient = new HttpClient(new HttpClientHandler
		{
			UseCookies = true,
			CookieContainer = new CookieContainer(),
		})
		{
			Timeout = options.Timeout
		};
		_jsonSerializerOptions = new JsonSerializerOptions()
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
		};
	}
	public async Task<DeviceInfo> GetDeviceInfo()
	{
		await Authenticate();

		var uri = new Uri(BaseUri, $"app?token={Token}");

		var response = await SecurePassThrough<GetDeviceInfoRequest, GetDeviceInfoResponse>(uri, new GetDeviceInfoRequest());

		return response.Result;
	}

	public async Task<bool> IsTurnedOn() => (await GetDeviceInfo()).DeviceOn;

	public Task TurnOff() => SetDeviceInfo(false);
	public Task TurnOn() => SetDeviceInfo(true);

	private async Task SetDeviceInfo(bool turnOn)
	{
		await Authenticate();

		var uri = new Uri(BaseUri, $"app?token={Token}");

		_ = await SecurePassThrough<SetDeviceInfoRequest, SetDeviceInfoResponse>(uri,new SetDeviceInfoRequest(turnOn));
	}

	protected async Task Authenticate()
	{
		if (IsAuthenticated())
		{
			return;
		}

		await Handshake();
		await Login();
	}

	private bool IsAuthenticated() => !string.IsNullOrEmpty(Token);

	private async Task Handshake()
	{
		var uri = new Uri(BaseUri, "/app");

		using var rsa = RSA.Create(1024);
		var publicKey =
		$"-----BEGIN PUBLIC KEY-----\n" +
		Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()) + "\n" +
		"-----END PUBLIC KEY-----\n";

		var response = await PostAsJsonAsync<HandshakeRequest, HandshakeResponse>(uri, new HandshakeRequest(publicKey));

		if (response.Key is null)
		{
			throw AuthenticationException.HandshakeFailure(response.Error_Code);
		}

		var keyBytes = Convert.FromBase64String(response.Key);
		var keyParts = rsa.Decrypt(keyBytes, RSAEncryptionPadding.Pkcs1);
		var key = keyParts.AsSpan()[..16].ToArray();
		var iv = keyParts.AsSpan()[16..32].ToArray();

		var aes = Aes.Create();
		_encryptor = aes.CreateEncryptor(key, iv);
		_decryptor = aes.CreateDecryptor(key, iv);
	}

	private async Task Login()
	{
		var uri = new Uri(BaseUri, "/app");
		var response = await SecurePassThrough<LoginRequest, LoginResponse>(uri, new LoginRequest(_username, _password));
		if (response?.Result is null)
		{
			throw AuthenticationException.LoginFailure();
		}

		if (string.IsNullOrEmpty(response.Result?.Token))
		{
			throw AuthenticationException.LoginFailure(response.ErrorCode);
		}

		Token = response.Result.Value.Token;
	}

	protected async Task<TResponse> PostAsJsonAsync<TRequest, TResponse>(Uri uri, TRequest request)
	{
		var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
		var content = new StringContent(json, Encoding.UTF8, "application/json");

		var httpResponse = await _httpClient.PostAsync(uri.ToString(), content);
		var httpContent = await httpResponse.Content.ReadAsStringAsync();

		var response = JsonSerializer.Deserialize<TResponse>(httpContent, _jsonSerializerOptions);

		return response ?? throw new Exception("Failed");
	}

	protected async Task<TResponse> SecurePassThrough<TRequest, TResponse>(Uri uri, TRequest request)
	{
		if (_encryptor is null || _decryptor is null)
		{
			throw new InvalidOperationException("Unauthenticated call to SecurePassThrough");
		}
		var serializedRequest = JsonSerializer.Serialize(request, _jsonSerializerOptions);
		var requestBytes = Encoding.UTF8.GetBytes(serializedRequest);

		var encryptedRequest = _encryptor.TransformFinalBlock(requestBytes, 0, requestBytes.Length);
		var securePassthroughRequest = new SecurePassthroughRequest(Convert.ToBase64String(encryptedRequest));

		var response = await PostAsJsonAsync<SecurePassthroughRequest, SecurePassthroughResponse>(uri, securePassthroughRequest);

		var content = Convert.FromBase64String(response.Result.Response);
		var decrypted = _decryptor.TransformFinalBlock(content, 0, content.Length);
		var decryptedContent = Encoding.UTF8.GetString(decrypted);
		Console.WriteLine(decryptedContent);

		var deserializedResult = JsonSerializer.Deserialize<TResponse>(decryptedContent, _jsonSerializerOptions);

		return deserializedResult ?? throw new Exception($"Failed to parse {decryptedContent} into type {typeof(TRequest)}");
	}

	private abstract record Request(string Method);

	private sealed record HandshakeRequest : Request
	{
		public sealed record Payload(string Key);

		public Payload Params { get; }

		public HandshakeRequest(string publicKey) : base("handshake")
		{
			Params = new Payload(publicKey);
		}
	}

	private sealed record HandshakeResponse
	{
		public record struct Payload(string Key);

		public int Error_Code { get; init; }
		public Payload? Result { get; init; }

		public string? Key => Result?.Key;
	}

	private sealed record LoginRequest : Request
	{
		public record struct Payload(string Username, string Password);

		public Payload Params { get; }
		public long RequestTimeMils { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		public LoginRequest(string username, string password) : base("login_device")
		{
			Params = new Payload(username, password);
		}
	}

	private sealed record LoginResponse
	{
		public record struct Payload(string Token);

		public int ErrorCode { get; init; }
		public Payload? Result { get; init; }
	}

	private sealed record SecurePassthroughRequest : Request
	{
		public record Payload(string Request);

		public Payload Params { get; }

		public SecurePassthroughRequest(string request) : base("securePassthrough")
		{
			Params = new Payload(request);
		}
	}

	private sealed record SecurePassthroughResponse
	{
		public record struct Payload(string Response);

		public int ErrorCode { get; init; }
		public Payload Result { get; init; }
	}

	private sealed record SetDeviceInfoRequest : Request
	{
		public record Payload(bool DeviceOn);

		public Payload Params { get; }

		[JsonPropertyName("TerminalUUID")]
		public Guid TerminalUUID { get; } = Guid.NewGuid();

		public SetDeviceInfoRequest(bool deviceOn) : base("set_device_info")
		{
			Params = new Payload(deviceOn);
		}
	}

	private sealed record SetDeviceInfoResponse;

	private sealed record GetDeviceInfoRequest : Request
	{
		public GetDeviceInfoRequest() : base("get_device_info") { }
	}

	private sealed record GetDeviceInfoResponse(int Error_Code, DeviceInfo Result) { }

	private class SnakeCaseNamingPolicy : JsonNamingPolicy
	{
		public static SnakeCaseNamingPolicy Instance { get; } = new SnakeCaseNamingPolicy();

		public override string ConvertName(string name)
		{
			return String
		  .Concat(
			name.Select((x, i) =>
			  i > 0 && char.IsUpper(x) ?
				"_" + x.ToString() :
				x.ToString()))
		  .ToLower();
		}
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_httpClient.Dispose();
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
