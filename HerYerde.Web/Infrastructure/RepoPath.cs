namespace HerYerde.Web.Infrastructure;

/// <summary>--ithal yolları depo köküne göre yazılır; dotnet run çalışma dizinini proje klasörüne alır.</summary>
public static class RepoPath
{
    public static string Root(string contentRoot)
    {
        var directory = new DirectoryInfo(contentRoot);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HerYerde.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? contentRoot;
    }

    /// <summary>Mutlak ya da çalışma dizininde var olan yol olduğu gibi kalır; kalanı depo köküne bağlanır.</summary>
    public static string Resolve(string root, string path)
        => Path.IsPathRooted(path) || Directory.Exists(path) || File.Exists(path) ? path : Path.Combine(root, path);
}
