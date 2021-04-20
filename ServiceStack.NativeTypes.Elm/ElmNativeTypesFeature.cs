using ServiceStack;
using ServiceStack.NativeTypes;

public class ElmNativeTypesFeature : IPlugin
{
    void IPlugin.Register(IAppHost appHost)
    {
        appHost.GetContainer().RegisterAutoWired<ElmNativeTypesService>();
        appHost.RegisterService<ElmNativeTypesService>();
    }
}