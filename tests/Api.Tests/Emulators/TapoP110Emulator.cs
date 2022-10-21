namespace Api.Tests.Emulators;

using System;
using System.Collections.Generic;
using System.Linq;

using System.Net;
using System.IO;
using System.Threading.Tasks;

using System.Text;
using System.Text.Json;

using System.Security.Cryptography;
using System.Text.Encodings.Web;

public class TapoP110Emulator
{
  private readonly Dictionary<string, Func<HttpListenerRequest, JsonElement, string>> _methodHandlers;

  private HttpListener? _listener;

  private readonly string _username;
  private readonly string _password;

  private ICryptoTransform? _encryptor;
  private ICryptoTransform? _decryptor;

  private string? _token;
  private bool _isTurnedOn = false;

  public TapoP110Emulator(string username, string password)
  {
    _username = username;
    _password = password;
    _methodHandlers = new()
    {
      { "handshake", Handshake },
      { "securePassthrough", SecurePassthrough },
      { "login_device", LoginDevice},
      { "set_device_info", SetDeviceInfo}
    };
  }

  public bool IsOn() => _isTurnedOn;
  public bool IsOff() => !_isTurnedOn;

  public string Start()
  {
    int port = 3000;
    for(; port <= 4000; ++port)
    {
      try
      {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/app/");
        _listener.Start();
        break;
      }
      catch
      {
        _listener = null;
      }
    }

    if(_listener is null)
    {
      throw new Exception("Failed to locate a port to listen on");
    }

    Task.Run(ProcessRequests);

    return $"localhost:{port}";
  }

  public void Stop() => _listener?.Stop();

  private async Task ProcessRequests()
  {
    try
    {
      while(_listener!.IsListening)
      {
        var context = await _listener!.GetContextAsync();

        try
        {
          await WriteResponse(await GetResponse(context.Request), context.Response);
        }
        catch(Exception e)
        {
          await WriteResponse(e.ToString(), context.Response);
          //await WriteResponse("<html><body><center>200 OK</center></body></html>", context.Response);
        }
      }
    }
    catch
    {
      // ignored
    }
  }

  private async Task WriteResponse(string content, HttpListenerResponse response)
  {
      Console.WriteLine($"writing response {content}");
      var stream = response.OutputStream;
      var writer = new StreamWriter(stream);
      await writer.WriteAsync(content);
      writer.Close();
  }

  private async Task<string> GetResponse(HttpListenerRequest request)
  {
      var jsonDocument = await JsonDocument.ParseAsync(request.InputStream);

      var method = jsonDocument.RootElement.GetProperty("method").GetString();

      if(method is null || !_methodHandlers.ContainsKey(method))
      {
        throw new Exception();
      }

      Console.WriteLine($"processing {method} request");

      return _methodHandlers[method](request, jsonDocument.RootElement);
  }

  private string Handshake(HttpListenerRequest request, JsonElement requestJson)
  {
    var key = requestJson.GetProperty("params").GetProperty("key").GetString();
    if(key is null)
    {
        throw new Exception();
    }

    using(var aes = Aes.Create())
    {
      aes.KeySize = 128;

      aes.GenerateKey();
      aes.GenerateIV();

      Console.WriteLine(Convert.ToHexString(aes.Key));
      Console.WriteLine(Convert.ToHexString(aes.IV));

      _encryptor = aes.CreateEncryptor();
      _decryptor = aes.CreateDecryptor();

      using(var rsa = RSA.Create(1024))
      {
        rsa.ImportFromPem(key);

        var responseKey = new byte[32];
        aes.Key.CopyTo(responseKey, 0);
        aes.IV.CopyTo(responseKey, 16);

        var result = rsa.Encrypt(responseKey, RSAEncryptionPadding.Pkcs1);
        return Serialize(new HandshakeResponse(0, new HandshakeResponse.Payload(Convert.ToBase64String(result))));
      }
    }
  }

  private string LoginDevice(HttpListenerRequest request, JsonElement requestJson)
  {
    var username = requestJson.GetProperty("params").GetProperty("username").GetString();
    var password = requestJson.GetProperty("params").GetProperty("password").GetString();

    if(username is null || password is null)
    {
      throw new Exception();
    }

    if(Encoding.UTF8.GetString(Convert.FromBase64String(password)) != _password)
    {
      throw new Exception();
    }

    var usernameInHash = Convert.FromHexString(Encoding.UTF8.GetString(Convert.FromBase64String(username)));

    using(var sha1 = SHA1.Create())
    {
      var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(_username));

      if(!hash.SequenceEqual(usernameInHash))
      {
        throw new Exception();
      }
    }

    _token = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));

    return Serialize(new LoginResponse(0, new LoginResponse.Payload(_token)));
  }

  private string SetDeviceInfo(HttpListenerRequest request, JsonElement requestJson)
  {
    ValidateToken(request);

    var deviceOn = requestJson.GetProperty("params").GetProperty("device_on").GetBoolean();

    this._isTurnedOn = deviceOn;

    return "{ \"error_code\": 0}";
  }

  private void ValidateToken(HttpListenerRequest request)
  {
    var token = request.QueryString.Get("token");
    if(token is null || token != _token)
    {
      throw new Exception();
    }
  }

  private string SecurePassthrough(HttpListenerRequest request, JsonElement requestJson)
  {
    if(_decryptor is null || _encryptor is null)
    {
      throw new Exception();
    }
    var requestContent = requestJson.GetProperty("params").GetProperty("request").GetString();
    if(requestContent is null)
    {
      throw new Exception();
    }

    var encryptedContentBytes = Convert.FromBase64String(requestContent);
    var decrypted = _decryptor.TransformFinalBlock(encryptedContentBytes, 0, encryptedContentBytes.Length);


    Console.WriteLine(Encoding.UTF8.GetString(decrypted));

    var json = JsonDocument.Parse(decrypted);

    var method = json.RootElement.GetProperty("method").GetString();

    if(method is null || !_methodHandlers.ContainsKey(method))
    {
      Console.WriteLine($"method not found: {method}");
      throw new Exception();
    }

    var responseStr = _methodHandlers[method](request, json.RootElement);
    Console.WriteLine(responseStr);

    var response = Encoding.UTF8.GetBytes(responseStr);

    var encryptedResponse = _encryptor.TransformFinalBlock(response, 0, response.Length);

    var content = Convert.ToBase64String(encryptedResponse);

    return Serialize(new SecurePassthroughResponse(0, new SecurePassthroughResponse.Payload(content)));
  }

  private static string Serialize<TResponse>(TResponse response)
  {
      var options = new JsonSerializerOptions()
      {
          Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
          PropertyNamingPolicy = new SnakeCaseNamingPolicy()
      };

      return JsonSerializer.Serialize(response, options);
  }

  private record struct HandshakeResponse(int Error_Code, HandshakeResponse.Payload Result)
  {
    public record struct Payload(string Key);
  }

  private record struct SecurePassthroughResponse(int Error_Code, SecurePassthroughResponse.Payload Result)
  {
    public record struct Payload(string Response);
  }

  private sealed record LoginResponse(int Error_Code, LoginResponse.Payload Result)
  {
    public record struct Payload(string Token);
  }

  private sealed record SetDeviceInfoRequest
  {
    public record Payload(bool Device_On);

    public string Method { get; } = "set_device_info";
    public Payload Params { get; }
    public Guid TerminalUUID { get; } = Guid.NewGuid();

    public SetDeviceInfoRequest(bool deviceOn)
    {
      Params = new Payload(deviceOn);
    }
  }
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
}
