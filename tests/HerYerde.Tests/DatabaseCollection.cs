namespace HerYerde.Tests;

/// <summary>Tüm veritabanı testleri tek gerçek veritabanını paylaşır; sıralı koşarlar.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabaseCollection
{
    public const string Name = "database";
}
