if (args is ["context", "show"])
{
    Console.WriteLine("desktop-linux");
    return 0;
}

if (args.Length >= 3 && args[0] == "--context" && args[2] == "info")
{
    Console.WriteLine(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "engine-id.txt")));
    return 0;
}

Console.Error.WriteLine("Unsupported fake Docker CLI invocation.");
return 2;
