# Tapo
C# interface to the TP-Link Tapo P100 and P110 smart plugs


```csharp
var tapo = new TapoP110(new DeviceOptions("192.168.1.101", "username", "password"));

if(await tapo.IsTurnedOn())
{
    await tapo.TurnOff();
}
else
{
    await tapo.TurnOn();
}

var deviceInfo = await tapo.GetDeviceInfo();

Console.WriteLine(deviceInfo);

var energyUsage = await tapo.GetEnergyUsage();

Console.WriteLine(energyUsage);
```
