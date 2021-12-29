using AzureCloudServiceConfigurationGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Moq;

namespace AzureCloudServiceConfigurationGeneratorTests;

[TestFixture]
public class ConfigurationSourceGeneratorTests
{
    [Test]
    public async Task NoWorkerRoles()
    {
        await RunGenerator(Array.Empty<string>()).ConfigureAwait(false);
    }

    [Test]
    public async Task WorkerRoleWithNoConfiguration()
    {
        await RunGenerator(new[]
        {
            @"
<WorkerRole name='MyWorkerRole' vmsize='Standard_D1_v2'>
</WorkerRole>"
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task WorkerRoleWithConfigurationSettings()
    {
        await RunGenerator(new[]
        {
            @"
<WorkerRole name='MyWorkerRole' vmsize='Standard_D1_v2'>
  <ConfigurationSettings>
    <Setting name='Setting1' />
    <Setting name='Setting2' />
  </ConfigurationSettings>
</WorkerRole>"
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task WorkerRoleWithLocalStorage()
    {
        await RunGenerator(new[]
        {
            @"
<WorkerRole name='MyWorkerRole' vmsize='Standard_D1_v2'>
  <LocalResources>
    <LocalStorage name='storage1' cleanOnRoleRecycle='true' sizeInMB='100' />
    <LocalStorage name='storage2' cleanOnRoleRecycle='true' sizeInMB='100' />
  </LocalResources>
</WorkerRole>"
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task WorkerRoleWithConfigurationSettingsAndLocalStorage()
    {
        await RunGenerator(new[]
        {
            @"
<WorkerRole name='MyWorkerRole' vmsize='Standard_D1_v2'>
  <ConfigurationSettings>
    <Setting name='Setting1' />
    <Setting name='Setting2' />
  </ConfigurationSettings>
  <LocalResources>
    <LocalStorage name='storage1' cleanOnRoleRecycle='true' sizeInMB='100' />
    <LocalStorage name='storage2' cleanOnRoleRecycle='true' sizeInMB='100' />
  </LocalResources>
</WorkerRole>"
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task OverrideNamespace()
    {
        await RunGenerator(new[]
        {
            @"
<WorkerRole name='MyWorkerRole' vmsize='Standard_D1_v2'>
</WorkerRole>"
        }, "CustomNamespace").ConfigureAwait(false);
    }

    [Test]
    public async Task RoleNameWithDots()
    {
        await RunGenerator(new[]
        {
            @"
<WorkerRole name='My.Worker.Role' vmsize='Standard_D1_v2'>
</WorkerRole>"
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task MultipleWorkerRoles()
    {
        await RunGenerator(new[]
        {
            @"
<WorkerRole name='WorkerRole1' vmsize='Standard_D1_v2'>
  <ConfigurationSettings>
    <Setting name='Setting1' />
    <Setting name='Setting2' />
  </ConfigurationSettings>
  <LocalResources>
    <LocalStorage name='storage1' cleanOnRoleRecycle='true' sizeInMB='100' />
    <LocalStorage name='storage2' cleanOnRoleRecycle='true' sizeInMB='100' />
  </LocalResources>
</WorkerRole>",
            @"
<WorkerRole name='WorkerRole2' vmsize='Standard_D1_v2'>
  <ConfigurationSettings>
    <Setting name='Setting2' />
    <Setting name='Setting3' />
  </ConfigurationSettings>
  <LocalResources>
    <LocalStorage name='storage2' cleanOnRoleRecycle='true' sizeInMB='100' />
    <LocalStorage name='storage3' cleanOnRoleRecycle='true' sizeInMB='100' />
  </LocalResources>
</WorkerRole>"
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task FilterWorkerRole()
    {
        await RunGenerator(new[]
        {
            @"
<WorkerRole name='WorkerRole1' vmsize='Standard_D1_v2'>
  <ConfigurationSettings>
    <Setting name='Setting1' />
    <Setting name='Setting2' />
  </ConfigurationSettings>
  <LocalResources>
    <LocalStorage name='storage1' cleanOnRoleRecycle='true' sizeInMB='100' />
    <LocalStorage name='storage2' cleanOnRoleRecycle='true' sizeInMB='100' />
  </LocalResources>
</WorkerRole>",
            @"
<WorkerRole name='WorkerRole2' vmsize='Standard_D1_v2'>
  <ConfigurationSettings>
    <Setting name='Setting2' />
    <Setting name='Setting3' />
  </ConfigurationSettings>
  <LocalResources>
    <LocalStorage name='storage2' cleanOnRoleRecycle='true' sizeInMB='100' />
    <LocalStorage name='storage3' cleanOnRoleRecycle='true' sizeInMB='100' />
  </LocalResources>
</WorkerRole>"
        }, roleName: "WorkerRole2").ConfigureAwait(false);
    }

    private async Task RunGenerator(IEnumerable<string> workerRoles, string? configuredNamespace = null,
        string? roleName = null)
    {
        var referenceAssemblies = await ReferenceAssemblies.Default
            .ResolveAsync(LanguageNames.CSharp, CancellationToken.None).ConfigureAwait(false);
        var assemblies = referenceAssemblies;

        var compilation =
            CSharpCompilation.Create("MyGeneratedCodeNamepace", Enumerable.Empty<SyntaxTree>(), assemblies);
        var generator = new ConfigurationSourceGenerator();

        var mockOptions = new Mock<AnalyzerConfigOptions>(MockBehavior.Strict);
        mockOptions.Setup(mock => mock.TryGetValue("build_metadata.AdditionalFiles.Namespace", out configuredNamespace))
            .Returns(!string.IsNullOrWhiteSpace(configuredNamespace));
        mockOptions.Setup(mock => mock.TryGetValue("build_metadata.AdditionalFiles.RoleName", out roleName))
            .Returns(!string.IsNullOrWhiteSpace(roleName));

        var mockOptionsProvider = new Mock<AnalyzerConfigOptionsProvider>(MockBehavior.Strict);
        mockOptionsProvider.Setup(mock => mock.GetOptions(It.IsAny<AdditionalText>()))
            .Returns(mockOptions.Object);

        var mockAdditionalText = new Mock<AdditionalText>(MockBehavior.Strict);
        mockAdditionalText.Setup(mock => mock.Path).Returns("ServiceDefinition.csdef");
        var xmlFile = $@"
<?xml version='1.0' encoding='utf-8'?>
<ServiceDefinition name='CloudService' xmlns='http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition' schemaVersion='2015-04.2.6'>
{string.Join("\r\n", workerRoles.Select(workerRole => workerRole.Trim()))}
</ServiceDefinition>".Trim();
        mockAdditionalText.Setup(mock => mock.GetText(It.IsAny<CancellationToken>())).Returns(SourceText.From(xmlFile));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { generator },
            new[] { mockAdditionalText.Object }, optionsProvider: mockOptionsProvider.Object);
        driver = driver.RunGenerators(compilation);
        await Verify(driver)
            .UseDirectory("Snapshots")
            .ScrubLinesContaining("System.CodeDom.Compiler.GeneratedCode")
            .ConfigureAwait(false);
    }
}