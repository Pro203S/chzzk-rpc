namespace DischeeseServer;

internal sealed class SingleInstanceLock : IDisposable
{
    private readonly FileStream lockFile;

    private SingleInstanceLock(FileStream lockFile)
    {
        this.lockFile = lockFile;
    }

    public static SingleInstanceLock? TryAcquire()
    {
        string directoryPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            ),
            "DischeeseServer"
        );

        Directory.CreateDirectory(directoryPath);

        string lockPath = Path.Combine(
            directoryPath,
            "server.lock"
        );

        try
        {
            FileStream lockFile = new(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.None
            );

            return new SingleInstanceLock(lockFile);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        lockFile.Dispose();
    }
}
