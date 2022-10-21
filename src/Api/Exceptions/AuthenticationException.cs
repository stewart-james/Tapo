namespace Api.Exceptions;

public class AuthenticationException : Exception
{
  public AuthenticationException(){}
  public AuthenticationException(string message) : base(message){}

  public static AuthenticationException HandshakeFailure(int errorCode) =>
    new AuthenticationException($"Failed to perform handshake {errorCode}");

  public static AuthenticationException HandshakeFailure() =>
    new AuthenticationException($"Failed to perform handshake");

  public static AuthenticationException LoginFailure() =>
      new AuthenticationException($"Failed to login to device");

  public static AuthenticationException LoginFailure(int errorCode) =>
      new AuthenticationException($"Failed to login to device {errorCode}");
} 
