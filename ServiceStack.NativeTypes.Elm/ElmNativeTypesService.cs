using System;
using System.Collections.Generic;
using ServiceStack.DataAnnotations;
using ServiceStack.Host;
using ServiceStack.NativeTypes.Elm;
using ServiceStack.Web;
using TeTo.Entities.Shared;

namespace ServiceStack.NativeTypes
{

    [Exclude(Feature.Soap)]
    [Route("/types/elm")]
    public class TypesElm : NativeTypesBase { }


    [Restrict(VisibilityTo = RequestAttributes.None)]
    public class ElmNativeTypesService : Service
    {
        // Provided by NativeTypesService and can be removed on merge upstream.
        public INativeTypesMetadata NativeTypesMetadata { get; set; }


        [AddHeader(ContentType = MimeTypes.PlainText)]
        public object Any(TypesElm request)
        {
            if (request.BaseUrl == null)
                request.BaseUrl = Request.GetBaseUrl();

            var typesConfig = NativeTypesMetadata.GetConfig(request);
            // Avoid setting the Maybe attribute in the global configuration, by making
            // a copy and setting it there only.
            typesConfig = typesConfig.ToJson().FromJson<MetadataTypesConfig>();
            typesConfig.ExportAttributes.Add(typeof(MaybeAttribute));

            //Include SS types by removing ServiceStack namespaces
            if (typesConfig.AddServiceStackTypes)
                typesConfig.IgnoreTypesInNamespaces = new List<string>();

            // Code from private NativeTypesService.ExportMissingSystemTypes(typesConfig);
            if (typesConfig.ExportTypes == null)
                typesConfig.ExportTypes = new HashSet<Type>();
            typesConfig.ExportTypes.Add(typeof(KeyValuePair<,>));
            // typesConfig.ExportTypes.Add(typeof(ValueTuple<>));
            // typesConfig.ExportTypes.Add(typeof(ValueTuple<,>));
            // typesConfig.ExportTypes.Add(typeof(ValueTuple<,,>));
            // typesConfig.ExportTypes.Add(typeof(ValueTuple<,,,>));

            // typesConfig.ExportTypes.Add(typeof(Tuple<>));
            // typesConfig.ExportTypes.Add(typeof(Tuple<,>));
            // typesConfig.ExportTypes.Add(typeof(Tuple<,,>));
            // typesConfig.ExportTypes.Add(typeof(Tuple<,,,>));

            var metadataTypes = NativeTypesMetadata.GetMetadataTypes(Request, typesConfig);

            metadataTypes.Types.RemoveAll(x => x.Name == "Service");

            var elm = new ElmGenerator(typesConfig).GetCode(metadataTypes, base.Request, NativeTypesMetadata);
            return elm;
        }


    }
}