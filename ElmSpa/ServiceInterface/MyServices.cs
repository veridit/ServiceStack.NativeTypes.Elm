namespace ElmSpa.ServiceInterface
{
    using ServiceStack;
    using ServiceModel;

    public class MyServices : Service
    {
        public object Any(Hello request)
        {
            return new HelloResponse { Result = $"Hello, {request.Name}!" };
        }
    }
}
