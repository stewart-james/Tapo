namespace Api.Tests;

using System.IO;
using Xunit.Abstractions;

public class ConsoleWriter : StringWriter
{
    private readonly ITestOutputHelper output;

    public ConsoleWriter(ITestOutputHelper output) => this.output = output;
    public override void WriteLine(string? m) => output.WriteLine(m);
}
