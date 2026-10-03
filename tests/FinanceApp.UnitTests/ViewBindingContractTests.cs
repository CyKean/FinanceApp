namespace FinanceApp.UnitTests;

using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

/// <summary>
/// Guards the two XAML mistakes that break a MAUI page at runtime while the
/// build stays green.
/// <para>
/// 1. A <c>DataTemplate</c> without <c>x:DataType</c> inherits the page's
///    <c>x:DataType</c>, so every binding inside it is compiled against the
///    ViewModel instead of the item type. The paths then do not exist and the
///    affected controls render empty - no exception, no warning. This is what
///    left Category Breakdown, Budget Forecasts and Smart Insights as bare
///    icons and empty progress bars.
/// </para>
/// <para>
/// 2. A <c>MarkupExtension</c> has to be the entire attribute value. Writing
///    <c>Text="Current: {Binding X}"</c> or <c>Text="{Binding A} - {Binding B}"</c>
///    prints the binding source as literal text on screen.
/// </para>
/// </summary>
public class ViewBindingContractTests
{
    private static readonly Regex BindingRegex =
        new(@"\{Binding\s+(?<path>[^,}]+)", RegexOptions.Compiled);

    /// <summary>StringFormat arguments carry their own braces, so strip them first.</summary>
    private static readonly Regex StringFormatRegex =
        new(@"StringFormat='[^']*'", RegexOptions.Compiled);

    /// <summary>Attributes whose value is shown to the user as text.</summary>
    private static readonly string[] TextAttributes =
    [
        "Text", "Title", "Subtitle", "Header", "HeaderTemplate", "Footer", "SemanticProperties.Description"
    ];

    private static string ViewsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "FinanceApp.Mobile", "Views");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/FinanceApp.Mobile/Views.");
    }

    private static IEnumerable<string> ViewFiles() =>
        Directory.EnumerateFiles(ViewsRoot(), "*.xaml", SearchOption.AllDirectories);

    private static XDocument Load(string path)
    {
        try
        {
            return XDocument.Load(path);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new Xunit.Sdk.XunitException($"{path} is not valid XML: {ex.Message}");
        }
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(ViewsRoot(), path).Replace('\\', '/');

    [Fact]
    public void Every_DataTemplate_declares_xDataType()
    {
        var offenders = new List<string>();

        foreach (var file in ViewFiles())
        {
            var doc = Load(file);
            var pageType = doc.Root?.Attribute(XamlNamespace + "DataType")?.Value;

            foreach (var template in doc.Descendants().Where(e => e.Name.LocalName == "DataTemplate"))
            {
                if (template.Attribute(XamlNamespace + "DataType") is null)
                    offenders.Add($"{Relative(file)}: <DataTemplate> has no x:DataType" +
                                  $" (would compile against '{pageType ?? "nothing"}')");
            }
        }

        Assert.True(offenders.Count == 0,
            "DataTemplates without x:DataType bind against the wrong type and render blank:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("Title")]
    [InlineData("Subtitle")]
    [InlineData("Header")]
    [InlineData("Footer")]
    public void No_text_attribute_embeds_a_binding(string attribute)
    {
        var offenders = new List<string>();

        foreach (var file in ViewFiles())
        {
            foreach (var element in Load(file).Descendants())
            {
                var value = element.Attribute(attribute)?.Value;
                if (value is null || !value.Contains("{Binding", StringComparison.Ordinal)) continue;

                if (IsSingleMarkupExtension(value)) continue;

                offenders.Add($"{Relative(file)}: {attribute}=\"{value}\" - a MarkupExtension must be the whole value");
            }
        }

        Assert.True(offenders.Count == 0,
            "Bindings embedded in a longer string render as literal text:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// True when the value is exactly one brace group, e.g.
    /// <c>{Binding X, Converter=Y}</c>. False for <c>Current: {Binding X}</c>
    /// and <c>{Binding A} - {Binding B}</c>, which XAML prints verbatim.
    /// </summary>
    private static bool IsSingleMarkupExtension(string value)
    {
        var trimmed = StringFormatRegex.Replace(value.Trim(), string.Empty);

        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}')) return false;

        var depth = 0;
        var groups = 0;

        foreach (var c in trimmed)
        {
            if (c == '{')
            {
                if (depth == 0) groups++;
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth < 0) return false;
            }
        }

        return depth == 0 && groups == 1;
    }

    [Fact]
    public void Every_bound_path_exists_on_its_declared_DataType()
    {
        var applicationAssembly = typeof(FinanceApp.Application.DTOs.PredictionResultDto).Assembly;
        var offenders = new List<string>();

        foreach (var file in ViewFiles())
        {
            var doc = Load(file);

            // Nearest enclosing x:DataType wins, matching XAMLC semantics.
            foreach (var element in doc.Descendants().Where(e => BindingRegex.IsMatch(e.Attribute("Text")?.Value ?? string.Empty)))
            {
                var owner = element.AncestorsAndSelf()
                    .FirstOrDefault(a => a.Attribute(XamlNamespace + "DataType") is not null);

                var dataTypeName = owner?.Attribute(XamlNamespace + "DataType")?.Value;
                if (dataTypeName is null) continue;

                var shortName = dataTypeName.Split(':')[^1];
                var ownerType = applicationAssembly.GetTypes().FirstOrDefault(t => t.Name == shortName);
                if (ownerType is null)
                    continue; // ViewModel or a project-local type; not in scope here.

                var path = BindingRegex.Match(element.Attribute("Text")!.Value).Groups["path"].Value.Trim();
                if (!PathIsValid(path, ownerType))
                    offenders.Add($"{Relative(file)}: '{path}' does not resolve on {ownerType.Name}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Bound property paths that do not exist on the item type:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// Walks each segment of a dotted path. Stops without complaining once it
    /// reaches a type it cannot introspect (a converter parameter, say).
    /// </summary>
    private static bool PathIsValid(string path, Type root)
    {
        var current = root;

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is null) return true;

            var property = current.GetProperty(segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (property is null) return false;
            current = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        }

        return true;
    }

    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";
}