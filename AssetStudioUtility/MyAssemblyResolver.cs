namespace AssetStudio;

using Mono.Cecil;

public class MyAssemblyResolver : DefaultAssemblyResolver
{
    public void Register(AssemblyDefinition assembly)
        => RegisterAssembly(assembly);
}