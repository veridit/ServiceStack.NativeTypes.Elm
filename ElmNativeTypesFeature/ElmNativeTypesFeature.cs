using ServiceStack.NativeTypes;

namespace ServiceStack
{
    public class ElmNativeTypesFeature : IPlugin
    {
        void IPlugin.Register(IAppHost appHost)
        {
            appHost.GetContainer().RegisterAutoWired<ElmNativeTypesService>();
            appHost.RegisterService<ElmNativeTypesService>();
        }
    }
}