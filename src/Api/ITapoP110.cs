namespace Api;

public interface ITapoP110 : ITapoP100
{
  Task<TapoP110EnergyUsage> GetEnergyUsage();
}

