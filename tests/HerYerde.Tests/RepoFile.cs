namespace HerYerde.Tests;

/// <summary>Depodaki bir dosyayı (ci.yml, input.css) test çıktı klasöründen yukarı çıkıp HerYerde.sln'in yanında bulur.</summary>
public static class RepoFile
{
    public static string ReadAllText(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HerYerde.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine([dir.FullName, .. parts]));
    }
}
