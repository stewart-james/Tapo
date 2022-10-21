namespace Api;

public record TapoP110EnergyUsage
(
  int TodayRuntime,
  int MonthRuntime,
  int TodayEnergy,
  int MonthEnergy,
  string LocalTime,
  int CurrentPower
);
