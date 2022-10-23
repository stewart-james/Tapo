namespace Api;

using System.Collections.Immutable;
using System.Globalization;

public class TapoP110 : TapoP100, ITapoP110
{
	public TapoP110(DeviceOptions options) : base(options)
	{
	}

	public async Task<TapoP110EnergyUsage> GetEnergyUsage()
	{
		await Authenticate();

		var response = await SecurePassThrough<GetEnergyUsageRequest, GetEnergyUsageResponse>(
		  AppTokenUri,
		  new GetEnergyUsageRequest());

		return new TapoP110EnergyUsage
		(
			response.Result.TodayRuntime,
			response.Result.MonthRuntime,
			response.Result.TodayEnergy,
			response.Result.MonthEnergy,
			DateTime.ParseExact(response.Result.LocalTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
			response.Result.CurrentPower,
			ImmutableArray.Create(response.Result.ElectricityCharge)
		);
	}

	public sealed record GetEnergyUsageRequest
	{
		public string Method { get; } = "get_energy_usage";
		public long RequestTimeMils { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
	}

	private sealed record GetEnergyUsageResponse(int ErrorCode, EnergyUsage Result);
	private sealed record EnergyUsage
	(
		int TodayRuntime,
		int MonthRuntime,
		int TodayEnergy,
		int MonthEnergy,
		string LocalTime,
		int[] ElectricityCharge,
		int CurrentPower
	);
}
