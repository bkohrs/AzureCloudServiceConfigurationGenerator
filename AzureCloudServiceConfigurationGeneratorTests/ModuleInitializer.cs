using System.Runtime.CompilerServices;

namespace AzureCloudServiceConfigurationGeneratorTests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Enable();
    }
}