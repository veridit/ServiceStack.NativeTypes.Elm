using System;
using ServiceStack;
using ElmSpa.ServiceModel;

namespace ElmSpa.ServiceInterface
{
    public class MyServices : Service
    {
        public object Any(Hello request)
        {
            return new HelloResponse { Result = $"Hello, {request.Name}!" };
        }
    }
}
