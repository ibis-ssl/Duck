namespace Duck.Testing.AppHost;

internal static class AutoRefGameLogAgent
{
    public static void RequireAvailable(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Match mode requires the AutoRef native game-log activation agent.",
                path);
        }
    }
}
