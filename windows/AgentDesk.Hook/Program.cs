using AgentDesk.Hook;

var env = Environment.GetEnvironmentVariables()
    .Cast<System.Collections.DictionaryEntry>()
    .ToDictionary(e => (string)e.Key, e => e.Value?.ToString(), StringComparer.OrdinalIgnoreCase);

using var httpClient = new HttpClient();
return await HookRunner.RunAsync(
    Console.OpenStandardInput(),
    Console.Out,
    Console.Error,
    env,
    httpClient);
