using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace AzureCloudServiceConfigurationGenerator;

[Generator]
public class ConfigurationSourceGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        var assemblyName = Assembly.GetExecutingAssembly().GetName();
        var xmlNamespace = "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition";
        foreach (var additionalFile in context.AdditionalFiles)
        {
            if (Path.GetExtension(additionalFile.Path) != ".csdef")
                continue;

            context.AnalyzerConfigOptions.GetOptions(additionalFile)
                .TryGetValue("build_metadata.AdditionalFiles.Namespace", out var configuredNamespace);
            context.AnalyzerConfigOptions.GetOptions(additionalFile)
                .TryGetValue("build_metadata.AdditionalFiles.RoleName", out var filteredRoleName);

            var namespaceName = configuredNamespace ?? context.Compilation.AssemblyName;
            var text = additionalFile.GetText(context.CancellationToken)?.ToString() ?? "";
            var xmlDoc = XDocument.Parse(text);
            var roles = xmlDoc.Descendants(XName.Get("WorkerRole", xmlNamespace));
            foreach (var role in roles)
            {
                var roleName = role.Attribute("name")?.Value.Replace(".", "_");
                if (string.IsNullOrEmpty(filteredRoleName) || filteredRoleName == roleName)
                {
                    var builder = new StringBuilder();
                    if (!string.IsNullOrWhiteSpace(namespaceName))
                        builder.AppendLine($"namespace {namespaceName};");
                    builder.AppendLine("");
                    builder.AppendLine(
                        $"[System.CodeDom.Compiler.GeneratedCode(\"{assemblyName.Name}\",\"{assemblyName.Version}\")]");
                    builder.AppendLine($"public static class {roleName}Configuration");
                    builder.AppendLine("{");
                    builder.AppendLine("    public static System.Collections.Generic.IEnumerable<string> Settings");
                    builder.AppendLine("    {");
                    builder.AppendLine("        get");
                    builder.AppendLine("        {");
                    var configSettings = role.Descendants(XName.Get("Setting", xmlNamespace)).ToImmutableList();
                    if (!configSettings.IsEmpty)
                    {
                        foreach (var configSetting in configSettings)
                        {
                            builder.AppendLine(
                                $"            yield return \"{configSetting.Attribute("name")?.Value}\";");
                        }
                    }
                    else
                    {
                        builder.AppendLine("            yield break;");
                    }

                    builder.AppendLine("        }");
                    builder.AppendLine("    }");
                    builder.AppendLine(
                        "    public static System.Collections.Generic.IEnumerable<string> LocalStorages");
                    builder.AppendLine("    {");
                    builder.AppendLine("        get");
                    builder.AppendLine("        {");
                    var localStorages = role.Descendants(XName.Get("LocalStorage", xmlNamespace)).ToImmutableList();
                    if (!localStorages.IsEmpty)
                    {
                        foreach (var localStorage in localStorages)
                        {
                            builder.AppendLine(
                                $"            yield return \"{localStorage.Attribute("name")?.Value}\";");
                        }
                    }
                    else
                        builder.AppendLine("            yield break;");

                    builder.AppendLine("        }");
                    builder.AppendLine("    }");
                    builder.AppendLine("}");
                    context.AddSource(roleName + "Configuration.cs", builder.ToString());
                }
            }
        }
    }

    public void Initialize(GeneratorInitializationContext context)
    {
    }
}