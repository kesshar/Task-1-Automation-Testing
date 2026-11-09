namespace ResilientFormTests;

/// <summary>
/// Lightweight loader for .env configuration files.
/// Searches the working directory and parent directories to locate a .env file,
/// parsing variables and injecting them into the environment without external dependencies.
/// Existing system/CLI environment variables take precedence over .env file values.
/// </summary>
public static class EnvLoader
{
    private static bool _loaded;
    private static readonly object Lock = new();

    public static void Load()
    {
        lock (Lock)
        {
            if (_loaded) return;
            _loaded = true;

            var startDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var envFile = FindEnvFile(startDir);
            if (envFile == null) return;

            foreach (var rawLine in File.ReadAllLines(envFile.FullName))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    continue;

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();

                // Remove surrounding quotes if present
                if ((value.StartsWith('"') && value.EndsWith('"')) ||
                    (value.StartsWith('\'') && value.EndsWith('\'')))
                {
                    value = value[1..^1];
                }

                // Only set if not already defined in the process environment (CLI overrides take precedence)
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }

    private static FileInfo? FindEnvFile(DirectoryInfo? dir)
    {
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
                return new FileInfo(candidate);

            dir = dir.Parent;
        }
        return null;
    }
}

