namespace Api;


public class TapoP110 : TapoP100, ITapoP110
{
  public TapoP110(DeviceOptions options) : base(options){}

  public async Task<TapoP110EnergyUsage> GetEnergyUsage()
  {
    await Authenticate();

    var response =await SecurePassThrough<GetEnergyUsageRequest, GetEnergyUsageResponse>(
        AppTokenUri,
        new GetEnergyUsageRequest());

    return response.Result;
  }

  public sealed record GetEnergyUsageRequest
  {
    public string Method { get; } = "get_energy_usage";
    public long RequestTimeMils { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  }
  private sealed record GetEnergyUsageResponse(int ErrorCode, TapoP110EnergyUsage Result);
}
