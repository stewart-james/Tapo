namespace Api;

using System.Collections.Immutable;

public record TapoP110EnergyUsage
(
	int TodayRuntime,
	int MonthRuntime,
	int TodayEnergy,
	int MonthEnergy,
	DateTime LocalTime,
	int CurrentPower,
	ImmutableArray<int> ElectricityCharge
);
