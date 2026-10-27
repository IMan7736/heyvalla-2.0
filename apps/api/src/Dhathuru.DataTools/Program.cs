// Ingestion CLI. KANDU owns this project. See Commands/Commands.cs for the command list.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
return await Dhathuru.DataTools.Commands.DataToolsCli.RunAsync(args, Console.Out, Console.Error, cts.Token);
