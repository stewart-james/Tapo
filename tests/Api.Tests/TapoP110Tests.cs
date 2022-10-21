using System;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

using Api.Tests.Emulators;

public class TapoP110Tests
{
    public TapoP110Tests(ITestOutputHelper output) => Console.SetOut(new ConsoleWriter(output));

    [Fact]
    public async Task Handshake()
    {
      var tapoEmulator = new TapoP110Emulator("root", "abc123");
      var address = tapoEmulator.Start();

      var tapo = new TapoP110(new DeviceOptions(address, "root", "abc123"));

      await tapo.TurnOn();
      Assert.True(tapoEmulator.IsOn());

      await tapo.TurnOff();
      Assert.True(tapoEmulator.IsOff());
    }
}
