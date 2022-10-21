namespace Api;

using System.Threading.Tasks;

public interface ITapoP100
{
  Task TurnOn();
  Task TurnOff();
}
