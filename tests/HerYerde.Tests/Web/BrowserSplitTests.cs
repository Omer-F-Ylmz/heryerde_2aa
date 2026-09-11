using System.Reflection;
using System.Text.RegularExpressions;

namespace HerYerde.Tests.Web;

/// <summary>Tarayıcı testleri Chrome indirir: build-test onları süzer, ayrı browser-check job'u haftalık ve elle koşar.</summary>
public sealed class BrowserSplitTests
{
    [Fact]
    public void PuppeteerSharp_kullanan_test_sinifi_Browser_kategorisi_tasir()
    {
        var browserClasses = typeof(BrowserSplitTests).Assembly.GetTypes()
            .Where(t => t.GetMethods().Any(m => m.IsDefined(typeof(FactAttribute)))
                && t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    .Any(f => f.FieldType.Assembly.GetName().Name == "PuppeteerSharp"))
            .ToList();

        Assert.NotEmpty(browserClasses);
        Assert.All(browserClasses, t => Assert.Contains(t.GetCustomAttributesData(), a =>
            a.AttributeType == typeof(TraitAttribute)
            && (string?)a.ConstructorArguments[0].Value == "Category"
            && (string?)a.ConstructorArguments[1].Value == "Browser"));
        Assert.DoesNotContain(typeof(Program).Assembly.GetReferencedAssemblies(), a => a.Name == "PuppeteerSharp");
    }

    [Fact]
    public void Ci_build_test_Browser_kategorisini_suzer_browser_check_haftalik_ve_elle_kosar()
    {
        var ci = RepoFile.ReadAllText(".github", "workflows", "ci.yml").ReplaceLineEndings("\n");

        Assert.Contains("\n  workflow_dispatch:", ci);
        Assert.Matches(new Regex(@"\n  schedule:\n(\s+#.*\n)*\s+- cron:"), ci);

        var buildTest = Job(ci, "build-test");
        Assert.Contains("dotnet test HerYerde.sln", buildTest);
        Assert.Contains("--filter \"Category!=Browser\"", buildTest);

        var browserCheck = Job(ci, "browser-check");
        Assert.Contains("github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'", browserCheck);
        Assert.Contains("--filter \"Category=Browser\"", browserCheck);
    }

    /// <summary>jobs altındaki iki boşluk girintili job bloğu; bir sonraki job'a ya da dosya sonuna kadar.</summary>
    private static string Job(string ci, string name)
    {
        var match = Regex.Match(ci, $@"\n  {Regex.Escape(name)}:\n(?<body>(?:(?:    .*)?\n)+?)(?=  \S|$)");
        Assert.True(match.Success, $"ci.yml içinde '{name}' job'u yok.");
        return match.Groups["body"].Value;
    }
}
