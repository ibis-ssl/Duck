using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

var productionPath = Path.GetFullPath(args[0]);
var evidencePath = Path.GetFullPath(args[1]);
var port = int.Parse(args[2]);
var productionDirectory = Path.GetDirectoryName(productionPath)!;
AssemblyLoadContext.Default.Resolving += (_, identity) =>
{
    var dependency = Path.Combine(productionDirectory, identity.Name + ".dll");
    return File.Exists(dependency) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(dependency) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(productionPath);
var type = assembly.GetType("Duck.Testing.RefereeDriver.Hosting.SimulatorBallControlClient", throwOnError: true)!;
var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
var constructor = type.GetConstructors(flags).Single();
var sender = constructor.Invoke([new IPEndPoint(IPAddress.Loopback, port), TimeSpan.FromSeconds(5)]);
const bool blueTeamOnPositiveHalf = true;
var payload = (byte[])type.GetMethod("CreateInitialOwnHalfSetupCommand", BindingFlags.Static | BindingFlags.NonPublic)!
    .Invoke(null, [blueTeamOnPositiveHalf])!;
var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var metadata = new
{
    productionPath,
    productionSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(productionPath))),
    endpoint = $"127.0.0.1:{port}",
    blueTeamOnPositiveHalf,
    payloadBytes = payload.Length,
    payloadSha256 = Convert.ToHexString(SHA256.HashData(payload)),
    observationSeconds = 5,
    sendMethodInvocationCount = 1,
};
File.WriteAllText(Path.Combine(evidencePath, "local-diagnostic-send-metadata.json"), JsonSerializer.Serialize(metadata, options));
// A second process invocation must fail before calling the production send method.
using (var once = new FileStream(Path.Combine(evidencePath, "local-diagnostic-send-once.lock"), FileMode.CreateNew))
using (var writer = new StreamWriter(once))
    writer.WriteLine(DateTimeOffset.UtcNow.ToString("O"));
var complete = type.GetMethod("CompleteInitialSetupObservationAsync", flags)!;
try
{
    var send = (Task)type.GetMethod("SendInitialOwnHalfSetupAsync", flags)!
        .Invoke(sender, [blueTeamOnPositiveHalf, CancellationToken.None])!;
    await send;
    File.WriteAllText(Path.Combine(evidencePath, "local-diagnostic-send-completed.log"), DateTimeOffset.UtcNow.ToString("O"));
    var capture = type.GetField("initialSetupResponse", flags)!.GetValue(sender)!;
    var observation = (Task)capture.GetType().GetProperty("Observation", flags)!.GetValue(capture)!;
    await observation;
    var finish = (Task)complete.Invoke(sender, null)!;
    await finish;
    var result = finish.GetType().GetProperty("Result")!.GetValue(finish)!;
    var json = JsonSerializer.Serialize(result, result.GetType(), options);
    File.WriteAllText(Path.Combine(evidencePath, "local-simulator-response.json"), json);
    Console.WriteLine(json);
}
finally
{
    await (Task)complete.Invoke(sender, null)!;
}
