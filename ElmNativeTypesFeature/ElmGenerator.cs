using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack;
using ServiceStack.Text;
using ServiceStack.Web;
using System.Text.RegularExpressions;
using OneOf;


// ReSharper disable once CheckNamespace
// Namespace reflects the final namespace in ServiceStack on successful merge upstream.
namespace ServiceStack.NativeTypes.Elm
{
    public abstract class ElmTypeDef : OneOfBase<
    ElmTypeDef.ElmRecord,
    ElmTypeDef.ElmUnion>
    {
        public class ElmRecord : ElmTypeDef
        {
            public string Name { get; set; }
            public List<ElmField> ElmFields { get; set; }
            public string Description { get; set; }
            public List<HttpRoute> HttpRoutes { get; set; }
            public ElmTypeExpr ReturnType { get; set; }
        }

        public class ElmUnion : ElmTypeDef
        {
            public string Name { get; set; }
            public string JsonKey { get; set; }
            public List<ElmConstructor> Constructors { get; set; }
            public UnionKind Kind { get; set; }
            public string Description { get; set; }
            public List<HttpRoute> HttpRoutes { get; set; }
            public ElmTypeExpr ReturnType { get; set; }
        }

        public enum UnionKind
        {
            CSharpEmulatedUnion,
            WorkaroundForRecursiveTypeWithOnlyOneConstructor,
            Enum
        }

        public class ElmConstructor
        {
            public string Constructor { get; set; }
            public string JsonKey { get; set; }
            // Only one of ContainsElmTypeName or ContainsAnonymousRecord must be set.
            public OneOf<OneOf.Types.None, List<ElmField>, ElmTypeExpr> Contains { get; set; }
            public string Description { get; set; }
        }

        public class ElmField
        {
            public string Label { get; set; }
            public string JsonKey { get; set; }
            public ElmTypeExpr ElmType { get; set; }
            public string Description { get; set; }
        }

        public class HttpRoute
        {
            public string Path { get; set; }
            public HttpVerb Verb { get; set; }
        }

        public enum HttpVerb
        {
            Get,
            Put,
            Post,
        }
        public enum ElmNameType
        {
            ConstructorOrType,
            FieldLabel
        }
    }
    public abstract class ElmTypeExpr : OneOfBase<
        ElmTypeExpr.ElmNamedType,
        ElmTypeExpr.ElmNamedGenericType,
        ElmTypeExpr.ElmMaybe,
        ElmTypeExpr.ElmList,
        ElmTypeExpr.ElmArray,
        ElmTypeExpr.ElmDict>
    {
        public class ElmNamedType : ElmTypeExpr
        {
            public string ElmName { get; set; }
        }
        public class ElmNamedGenericType : ElmTypeExpr
        {
            public string ElmName { get; set; }
            public List<ElmTypeExpr> args { get; set; }
        }
        public class ElmMaybe : ElmTypeExpr
        {
            public ElmTypeExpr arg { get; set; }
        }

        public class ElmDict : ElmTypeExpr
        {
            public ElmTypeExpr argKey { get; set; }
            public ElmTypeExpr argVal { get; set; }
        }
        public class ElmList : ElmTypeExpr
        {
            public ElmTypeExpr arg { get; set; }
        }
        public class ElmArray : ElmTypeExpr
        {
            public ElmTypeExpr arg { get; set; }
        }
    }



    public class ElmGenerator
    {
        readonly MetadataTypesConfig Config;
        List<string> conflictTypeNames = new List<string>();
        List<MetadataType> allTypes;
        Dictionary<string, ElmTypeDef> allElmTypes;

        public ElmGenerator(MetadataTypesConfig config)
        {
            Config = config;
        }

        public static string DefaultGlobalModule = "Dtos";


        public static string ElmHelpers = @"
type Guid
    = Guid String


guidStr : Guid -> String
guidStr (Guid guid) =
    guid


decodeGuid : JD.Decoder Guid
decodeGuid =
    JD.string
        |> JD.map Guid


encodeGuid : Guid -> JE.Value
encodeGuid (Guid guid) =
    JE.string guid


urlParseGuid : Url.Parser.Parser (Guid -> a) a
urlParseGuid =
    Url.Parser.custom ""GUID"" (Just << (\guid -> Guid guid))


safeRegex : String -> Regex
safeRegex =
    Regex.fromString >> Maybe.withDefault Regex.never


encodeParams : List ( String, String ) -> String
encodeParams params =
    let
        -- Remove any outermost double quotes from json to match the JSV expected by ServiceStack.
        jsvEncode str =
            let
                json2jsv jsonStr =
                    let
                        matcher match =
                            case match.submatches of
                                [ Maybe.Just subStr ] ->
                                    subStr

                                _ ->
                                    match.match
                    in
                    Regex.replace (safeRegex ""^\""(.*)\""$"") matcher jsonStr

                stripNull jsonStr =
                    let
                        matcher _ =
                            """"
                    in
                    Regex.replace (safeRegex ""^null$"") matcher jsonStr
            in
            Url.percentEncode <| json2jsv <| stripNull str
    in
    params
        |> List.map (\( key, val ) -> Url.percentEncode key ++ ""="" ++ jsvEncode val)
        |> String.join ""&""


type alias RequestDefaults requestDto responseDto msg =
    { method : String
    , headers : List Http.Header
    , path : String
    , urlBuilder : String -> requestDto -> String
    , bodyBuilder : requestDto -> Http.Body
    , expectBuilder : (Result Http.Error responseDto -> msg) -> Http.Expect msg
    , timeout : Maybe Float
    , tracker : Maybe String
    }


type alias Request msg =
    { method : String
    , headers : List Http.Header
    , url : String
    , body : Http.Body
    , expect : Http.Expect msg
    , timeout : Maybe Float
    , tracker : Maybe String
    }


requestFromDefaults : RequestDefaults requestDto responseDto msg -> requestDto -> (Result Http.Error responseDto -> msg) -> Request msg
requestFromDefaults defaults requestDto msgBuilder =
    { url = defaults.urlBuilder defaults.path requestDto
    , method = defaults.method
    , body = defaults.bodyBuilder requestDto
    , expect = defaults.expectBuilder msgBuilder
    , timeout = defaults.timeout
    , tracker = defaults.tracker
    , headers = defaults.headers
    }
";


        public static List<(string import, string package)> ElmImports = new List<(string, string)>
        {
            ("import Array exposing (Array)","elm/core"),
            ("import Dict exposing (Dict)","elm/core"),
            ("import Http","elm/http"),
            ("import Iso8601","rtfeldman/elm-iso8601-date-strings"),
            ("import Json.Decode as JD", "elm/json"),
            ("import Json.Decode.Extra as JDE", "elm-community/json-extra"),
            ("import Json.Decode.Pipeline as JDP", "NoRedInk/elm-decode-pipeline"),
            ("import Json.Encode as JE", "elm/json"),
            ("import Json.Encode.Extra as JEE", "elm-community/json-extra"),
            ("import Regex exposing (Regex)","elm/regex"),
            ("import Set exposing (Set)","elm/core"),
            ("import Time exposing (Posix)","elm/time"),
            ("import Url","elm/url"),
            ("import Url.Parser","elm/url"),
        };

        public static ConcurrentDictionary<string, string> ElmDecoders = new Dictionary<string, string>
        {
            {"String","JD.string"},
            {"Bool","JD.bool"},
            {"Char","JD.string"},
            {"Byte","JD.string"},
            {"Int","JD.int"},
            {"Float","JD.float"},
            {"Guid","decodeGuid"},
            {"Maybe","JD.nullable"},
            {"Posix","Iso8601.decoder"},
            {"List","JD.list"},
            {"Dict","JDE.dict2"},
            {"Array","JD.array"},
            {"Set","JDE.set"},
        }.ToConcurrentDictionary();


        public static ConcurrentDictionary<string, string> ElmEncoders =
            new Dictionary<string, string>
        {
            {"String", $"JE.string"},
            {"Bool", $"JE.bool"},
            {"Char", $"JE.string"},
            {"Byte", $"JE.string"},
            {"Int", $"JE.int"},
            {"Float", $"JE.float"},
            {"Guid", $"encodeGuid"},
            {"Posix", $"Iso8601.encode"}, // Hand coded beause it is missing from Json.Encode.Extra.
        }.ToConcurrentDictionary();


        public static ConcurrentDictionary<string, string> ElmToString =
            new Dictionary<string, string>
        {
            {"String", $"identity"},
            {"Bool", $"JE.bool"},
            {"Char", $"String.fromChar"},
            {"Int", $"String.fromInt"},
            {"Float", $"String.fromFloat"},
        }.ToConcurrentDictionary();



        public static ConcurrentDictionary<string, string> CSharpToElmMapping = new Dictionary<string, string>
        {
            {"String", "String"},
            {"Boolean", "Bool"},
            {"Char", "Char"},
            {"SByte", "Byte"},
            {"Byte", "Int"},
            {"Int16", "Int"},
            {"Int32", "Int"},
            {"Int64", "Int"},
            {"UInt16", "Int"},
            {"UInt32", "Int"},
            {"UInt64", "Int"},
            {"Single", "Float"},
            {"Double", "Float"},
            {"Decimal", "Float"},
            {"Guid", "Guid"},
            {"DateTime", "Posix"},
            {"DateTimeOffset", "Posix"},
            // {"TimeSpan", ""}, // Not supported in Elm.
            // {"Type", ""}, // Not supported in Elm.
            // {"List", "List"}, // Supported by ElmTypeExpr
            // {"Dictionary", "Dict"}, // Supported by ElmTypeExpr
            // {"Set", "Set"},
            {"Object", "String"}, // There is no null or any concept in Elm - rather map it to a string for custom parsing.
            // {"Stream", }, // Not supported in Elm.
        }.ToConcurrentDictionary();

        public static Func<List<MetadataType>, List<MetadataType>> FilterTypes = DefaultFilterTypes;

        public static List<MetadataType> DefaultFilterTypes(List<MetadataType> types) => types;

        public static string MaybeAttributeName = typeof(MaybeAttribute).Name.Replace("Attribute", String.Empty);

        public Dictionary<string, ElmTypeDef> BuildElmTypes(List<MetadataType> types)
        {
            var elmTypes = new Dictionary<string, ElmTypeDef>();

            ElmTypeExpr BuildReturnType(MetadataType type)
            {
                ElmTypeExpr result = null;
                if (type.ReturnMarkerTypeName != null)
                    result = ElmTypeExpression(type.ReturnMarkerTypeName.Name, type.ReturnMarkerTypeName.GenericArgs, type.Attributes);
                return result;
            }

            ElmTypeDef.HttpRoute BuildHttpRoute(MetadataRoute route)
            {
                var result1 = new ElmTypeDef.HttpRoute()
                {
                    Path = route.Path
                };

                ElmTypeDef.HttpVerb ParseRouteVerb(string route1)
                {
                    if (route1 == null || route1.Matches("GET"))
                        return ElmTypeDef.HttpVerb.Get;
                    else if (route1.Matches("POST"))
                        return ElmTypeDef.HttpVerb.Post;
                    else if (route1.Matches("PUT"))
                        return ElmTypeDef.HttpVerb.Put;
                    else
                        throw new Exception($"Unknown Http Verb {route1.ToJson()}");
                }

                result1.Verb = ParseRouteVerb(route.Verbs);
                return result1;
            }

            foreach (var type in types)
            {
                if (elmTypes.ContainsKey(type.Name))
                    continue;

                var elmTypeExpression = ElmTypeExpression(type.Name, type.GenericArgs, type.Attributes);
                var namedType = elmTypeExpression.AsT0;
                if (namedType == null)
                    throw new Exception("Top level type expressions must be ElmNamedType");
                ElmTypeDef elmType = null;

                if (type.IsEnum.GetValueOrDefault())
                {
                    ElmTypeDef.ElmConstructor BuildConstructor(string enumName, int enumIndex)
                    {
                        var jsonKey = enumName;
                        if (JsConfig.TreatEnumAsInteger || (type.Attributes.Safe().Any(x => x.Name == "Flags")))
                            jsonKey = type.EnumValues?[enumIndex];

                        return new ElmTypeDef.ElmConstructor()
                        {
                            Constructor = enumName.ToPascalCase(),
                            JsonKey = jsonKey,
                            Contains = new OneOf.Types.None(),
                        };
                    }

                    elmType = new ElmTypeDef.ElmUnion
                    {
                        Name = namedType.ElmName,
                        JsonKey = type.Name,
                        Constructors = type.EnumNames?.Zip(Enumerable.Range(0, type.EnumNames.Count), BuildConstructor)?.ToList(),
                        Kind = ElmTypeDef.UnionKind.Enum,
                        Description = type.Description,
                        HttpRoutes = type?.Routes?.Map(BuildHttpRoute),
                        ReturnType = BuildReturnType(type)
                    };
                }
                else
                {
                    var isRecursive = false;
                    if (type.Properties != null)
                    {
                        // Traverse all subtypes, until either the same type encountered,
                        // Or there is nothing to traverse.
                        void IsRecursive(TextNode node)
                        {
                            if (type.Name == node.Text)
                                isRecursive = true;
                            node.Children.Each(IsRecursive);
                        }
                        type.Properties.Each(child =>
                        {
                            child.GenericArgs.Each(arg =>
                                {
                                    IsRecursive(arg.TrimStart('\'').ParseTypeIntoNodes());
                                }
                            );
                        });
                    }

                    var isUnion = Regex.IsMatch(type.Name, @"^.*Union$");

                    var properties = type.Properties ?? new List<MetadataPropertyType>();
                    if (type.Inherits != null)
                    {
                        void AddInheritedProperties(MetadataType t)
                        {
                            if (t.Inherits != null)
                            {
                                var parentName = t.Inherits.Name.InheritedType();
                                var parentType = allTypes.FirstOrDefault(t2 => t2.Name == parentName);
                                var parentProperties = parentType?.Properties?.ToList() ?? new List<MetadataPropertyType>();
                                parentProperties.Each(parentProperty =>
                                {
                                    properties.AddIfNotExists(parentProperty);
                                });
                                AddInheritedProperties(parentType);
                            }
                        }
                        AddInheritedProperties(type);
                    }


                    // Filter out ResponseStatus and ignore Config.AddResponseStatus because errors are
                    // always handled in a separate error code path and not as part of the success path.
                    properties = properties.Safe().Where(x => x.Name != typeof(ResponseStatus).Name).ToList();

                    // default -> no change (on the wire), name.ToPascalCase() in Elm.
                    // Ignore JsConfig.EmitCamelCaseNames -> name.ToCamelCase()
                    // JsConfig.EmitLowercaseUnderscoreNames -> name.ToLowercaseUnderscore()


                    (string Name, string JsonKey) SafeName(string rawName, ElmTypeDef.ElmNameType nameType)
                    {
                        var fieldName = rawName.SafeToken();
                        // JSON encoding/decoding uses the configuration settings on the server.
                        var jsonKey = fieldName.PropertyStyle();
                        // The fieldname uses the regular Elm syntax.
                        switch (nameType)
                        {
                            case ElmTypeDef.ElmNameType.ConstructorOrType:
                                fieldName = fieldName.ToPascalCase();
                                break;
                            case ElmTypeDef.ElmNameType.FieldLabel:
                                fieldName = JsConfig.TextCase == TextCase.SnakeCase ? fieldName.ToLowercaseUnderscore() : fieldName.ToCamelCase();
                                break;
                            default:
                                throw new Exception($"Unknown ElmNameType {nameType.ToJson()}");
                        }
                        if (fieldName.IsKeyWord()) // TODO add a better strategy to avoid keyword name conflict.
                            fieldName = fieldName + "_";
                        return (Name: fieldName, jsonKey);
                    }

                    if (isUnion)
                    {
                        // ...Union types is C# construct that simulates a union
                        // type by only populating one of the properties,
                        // so each property is a different union type.
                        var elmUnion = new ElmTypeDef.ElmUnion
                        {
                            Constructors = new List<ElmTypeDef.ElmConstructor>(),
                            Kind = ElmTypeDef.UnionKind.CSharpEmulatedUnion,
                            Description = type.Description,
                            HttpRoutes = type?.Routes?.Map(BuildHttpRoute),
                            ReturnType = BuildReturnType(type)
                        };

                        (elmUnion.Name, _) = SafeName(namedType.ElmName, ElmTypeDef.ElmNameType.ConstructorOrType);

                        foreach (var prop in properties)
                        {
                            var elmConstructor = new ElmTypeDef.ElmConstructor();
                            (elmConstructor.Constructor, elmConstructor.JsonKey) = SafeName(prop.Name, ElmTypeDef.ElmNameType.ConstructorOrType);

                            var elmTypeName = ElmTypeExpression(prop.GetTypeName(Config, allTypes), prop.GenericArgs, prop.Attributes);
                            elmConstructor.Contains = elmTypeName;

                            elmUnion.Constructors.Add(elmConstructor);
                        }

                        elmType = elmUnion;
                    }
                    else if (isRecursive)
                    {
                        // Since record types can not be directly recursive in Elm,
                        // introduce a constructor that matches the class, and place
                        // the attributes as record properties for that constructor.
                        var elmFields = new List<ElmTypeDef.ElmField>();
                        var elmConstructor = new ElmTypeDef.ElmConstructor()
                        {
                            Contains = elmFields
                        };
                        var elmUnion = new ElmTypeDef.ElmUnion
                        {
                            Constructors = new List<ElmTypeDef.ElmConstructor> { elmConstructor },
                            Kind = ElmTypeDef.UnionKind.WorkaroundForRecursiveTypeWithOnlyOneConstructor,
                            Description = type.Description,
                            HttpRoutes = type?.Routes?.Map(BuildHttpRoute),
                            ReturnType = BuildReturnType(type)

                        };
                        elmType = elmUnion;

                        // Notice that JsonKey is null because there is nothing to parse,
                        // the json from the server will be encoded as a dict, because the
                        // union type is a workaround.
                        (elmUnion.Name, _) =
                            (elmConstructor.Constructor, _) =
                                SafeName(namedType.ElmName, ElmTypeDef.ElmNameType.ConstructorOrType);

                        foreach (var prop in properties)
                        {
                            var elmField = new ElmTypeDef.ElmField
                            {
                                Description = prop.Description
                            };
                            (elmField.Label, elmField.JsonKey) = SafeName(prop.Name, ElmTypeDef.ElmNameType.FieldLabel);

                            var elmTypeName = ElmTypeExpression(prop.GetTypeName(Config, allTypes), prop.GenericArgs, prop.Attributes);
                            elmField.ElmType = elmTypeName;

                            elmFields.Add(elmField);
                        }
                    }
                    else // not isRecursive && not isUnion
                    {
                        // TODO: Add implicit/explicit version handling.
                        /*
                        var addVersionInfo = Config.AddImplicitVersion != null && options.IsRequest;
                        if (addVersionInfo)
                        {
                            // TODO: Add version information during decoding from type to json, and not as part of the main structure.
                        }
                        */

                        var elmFields = new List<ElmTypeDef.ElmField>();
                        var elmRecord = new ElmTypeDef.ElmRecord
                        {
                            ElmFields = elmFields,
                            Description = type.Description,
                            HttpRoutes = type?.Routes?.Map(BuildHttpRoute),
                            ReturnType = BuildReturnType(type)
                        };
                        elmType = elmRecord;

                        (elmRecord.Name, _) = SafeName(namedType.ElmName, ElmTypeDef.ElmNameType.ConstructorOrType);

                        foreach (var prop in properties)
                        {
                            var elmField = new ElmTypeDef.ElmField
                            {
                                Description = prop.Description
                            };
                            (elmField.Label, elmField.JsonKey) = SafeName(prop.Name, ElmTypeDef.ElmNameType.FieldLabel);

                            var elmTypeName = ElmTypeExpression(prop.GetTypeName(Config, allTypes), prop.GenericArgs, prop.Attributes);
                            elmField.ElmType = elmTypeName;

                            elmFields.Add(elmField);
                        }
                    }

                    // TODO: Add attribute support by putting them as data in another record
                }

                elmTypes.Add(type.Name, elmType);
            }
            return elmTypes;
        }


        public string GetCode(MetadataTypes metadata, IRequest request, INativeTypesMetadata nativeTypes)
        {
            var typeNamespaces = new HashSet<string>();
            RemoveIgnoredTypes(metadata);
            metadata.Types.Each(x => typeNamespaces.Add(x.Namespace));
            metadata.Operations.Each(x => typeNamespaces.Add(x.Request.Namespace));

            var defaultImports = ElmImports.Select(items => items.import).ToList();
            var hasCustomImports = !Config.DefaultImports.IsEmpty();
            if (hasCustomImports)
            {
                defaultImports = Config.DefaultImports;
            }
            else
            {
                // TODO: Add default imports.
            }

            var defaultNamespace = Config.GlobalNamespace ?? DefaultGlobalModule;

            string DefaultValue(string k) => request.QueryString[k].IsNullOrEmpty() ? "--" : "";

            var sbInner = StringBuilderCache.Allocate();
            var sb = new StringBuilderWrapper(sbInner);
            sb.AppendLine("{- Options:");
            sb.AppendLine("     Date: {0}".Fmt(DateTime.Now.ToString("s").Replace("T", " ")));
            sb.AppendLine("     Version: {0}".Fmt(Env.ServiceStackVersion));
            sb.AppendLine("     Tip: {0}".Fmt(HelpMessages.NativeTypesDtoOptionsTip.Fmt("--")));
            sb.AppendLine("     BaseUrl: {0}".Fmt(Config.BaseUrl));
            sb.AppendLine();
            sb.AppendLine("   {0}Package: {1}".Fmt(DefaultValue("Package"), Config.Package));
            sb.AppendLine("   {0}GlobalNamespace: {1}".Fmt(DefaultValue("GlobalNamespace"), defaultNamespace));
            sb.AppendLine("   {0}AddPropertyAccessors: {1}".Fmt(DefaultValue("AddPropertyAccessors"), Config.AddPropertyAccessors));
            sb.AppendLine("   {0}SettersReturnThis: {1}".Fmt(DefaultValue("SettersReturnThis"), Config.SettersReturnThis));
            sb.AppendLine("   {0}AddServiceStackTypes: {1}".Fmt(DefaultValue("AddServiceStackTypes"), Config.AddServiceStackTypes));
            sb.AppendLine("   {0}AddResponseStatus: {1}".Fmt(DefaultValue("AddResponseStatus"), Config.AddResponseStatus));
            sb.AppendLine("   {0}AddDescriptionAsComments: {1}".Fmt(DefaultValue("AddDescriptionAsComments"), Config.AddDescriptionAsComments));
            sb.AppendLine("   {0}AddImplicitVersion: {1}".Fmt(DefaultValue("AddImplicitVersion"), Config.AddImplicitVersion));
            sb.AppendLine("   {0}IncludeTypes: {1}".Fmt(DefaultValue("IncludeTypes"), Config.IncludeTypes.Safe().ToArray().Join(",")));
            sb.AppendLine("   {0}ExcludeTypes: {1}".Fmt(DefaultValue("ExcludeTypes"), Config.ExcludeTypes.Safe().ToArray().Join(",")));
            sb.AppendLine("   {0}TreatTypesAsStrings: {1}".Fmt(DefaultValue("TreatTypesAsStrings"), Config.TreatTypesAsStrings.Safe().ToArray().Join(",")));
            sb.AppendLine("   {0}DefaultImports: {1}".Fmt(DefaultValue("DefaultImports"), defaultImports.Join(",")));
            sb.AppendLine("-}");
            sb.AppendLine();
            sb.AppendLine();

            sb.AppendLine("module {0} exposing (..)".Fmt(defaultNamespace.SafeToken()));
            sb.AppendLine();

            foreach (var typeName in Config.TreatTypesAsStrings.Safe())
            {
                CSharpToElmMapping[typeName] = "String";
            }

            if (Config.Package != null)
            {
                sb.AppendLine("import {0}".Fmt(Config.Package));
            }

            var existingTypes = new HashSet<string>();

            var requestTypes = metadata.Operations.Select(x => x.Request).ToHashSet();
            var requestTypesMap = metadata.Operations.ToSafeDictionary(x => x.Request);
            var responseTypes = metadata.Operations
                .Where(x => x.Response != null)
                .Select(x => x.Response).ToHashSet();
            var types = metadata.Types.ToHashSet();

            allTypes = new List<MetadataType>();
            allTypes.AddRange(requestTypes);
            allTypes.AddRange(responseTypes);
            allTypes.AddRange(types);
            // Elm only supports concrete types, so interfaces and abstract types makes no sense.
            allTypes.RemoveAll(t => t?.IsInterface ?? t?.IsAbstract ?? false);
            allTypes.Sort((a, b) => string.Compare(a.Name, b.Name));

            allTypes = FilterTypes(allTypes);

            //Avoid reusing same type name with different generic arity
            var conflictPartialNames = allTypes.Map(x => x.Name).Distinct()
                .GroupBy(g => g.LeftPart('`'))
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            this.conflictTypeNames = allTypes
                .Where(x => conflictPartialNames.Any(name => x.Name.StartsWith(name)))
                .Map(x => x.Name);

            allElmTypes = BuildElmTypes(allTypes);

            defaultImports.Each(l => sb.AppendLine(l));

            sb.AppendLine();
            sb.AppendLine(ElmHelpers);

            var firstEntry = true;
            foreach (var elmType in allElmTypes.OrderBy(kv => kv.Key).Select(kv => kv.Value))
            {
                if (!firstEntry)
                    sb.AppendLine();
                else
                    firstEntry = false;
                GenerateElmTypes(sb, elmType);
                GenerateElmJsonDecoder(sb, elmType);
                GenerateElmJsonEncoder(sb, elmType);
                GenerateElmHttpClient(sb, elmType);
                // TODO add record with default values.
                //GenerateElmMetadata(sb, elmType);
            }
            return StringBuilderCache.ReturnAndFree(sbInner);
        }

        private void GenerateElmTypes(StringBuilderWrapper sb, ElmTypeDef elmType)
        {
            sb.AppendLine();
            AppendComments(sb,
                elmType.Match
                    ((ElmTypeDef.ElmRecord elmRecord) => elmRecord.Description
                    , (ElmTypeDef.ElmUnion elmUnion) => elmUnion.Description
                )
            );

            elmType.Switch
                ((ElmTypeDef.ElmRecord elmRecord) =>
               {
                   var hasFields = (elmRecord.ElmFields?.Count ?? 0) > 0;
                   sb.AppendLine($"type alias {elmRecord.Name} =");
                   sb = sb.Indent();
                   if (hasFields)
                   {
                       var prefix = "{";
                       foreach (var elmField in elmRecord.ElmFields)
                       {
                           sb.AppendLine($"{prefix} {elmField.Label} : {ElmTypeExprToString(elmField.ElmType)}");
                           prefix = ",";
                       }
                       sb.AppendLine("}");
                   }
                   else
                   {
                       sb.AppendLine("{}");
                   }
                   sb = sb.UnIndent();
               }
                , (ElmTypeDef.ElmUnion elmUnion) =>
                {
                    sb.AppendLine($"type {elmUnion.Name}");
                    sb = sb.Indent();
                    var prefix = "=";
                    foreach (var elmConstructor in elmUnion.Constructors)
                    {
                        elmConstructor.Contains
                        .Switch((OneOf.Types.None _) => sb.AppendLine($"{prefix} {elmConstructor.Constructor}")
                                , (List<ElmTypeDef.ElmField> elmFields) =>
                                {
                                    sb.AppendLine($"{prefix} {elmConstructor.Constructor}");
                                    sb = sb.Indent();
                                    var innerPrefix = "{";
                                    foreach (var elmField in elmFields)
                                    {
                                        sb.AppendLine($"{innerPrefix} {elmField.Label} : {ElmTypeExprToString(elmField.ElmType)}");
                                        innerPrefix = ",";
                                    }
                                    sb.AppendLine("}");
                                    sb = sb.UnIndent();
                                }
                                , (ElmTypeExpr elmTypeExpr) =>
                                {
                                    sb.AppendLine($"{prefix} {elmConstructor.Constructor} {ElmTypeExprToString(elmTypeExpr, addParenthesisIfMultiple: true)}");
                                }
                                );
                        prefix = "|";
                    }
                    sb = sb.UnIndent();
                }
                );
        }


        private string ElmTypeExprToString(ElmTypeExpr elmTypeExpr, bool addParenthesisIfMultiple = false)
        {
            // Helper to invoke same method, always adding parenthesis,
            // as those can only be skipped for top level 
            string Self(ElmTypeExpr ete) => ElmTypeExprToString(ete, true);

            var result = elmTypeExpr.Match(
                (ElmTypeExpr.ElmNamedType elmType) => elmType.ElmName
                ,
                (ElmTypeExpr.ElmNamedGenericType elmGenType) =>
                    elmGenType.ElmName + " " + elmGenType.args.Select(arg => Self(arg)).Join(" ")
                ,
                (ElmTypeExpr.ElmMaybe elmMaybe) =>
                    "Maybe " + Self(elmMaybe.arg)
                ,
                (ElmTypeExpr.ElmList elmList) =>
                    "List " + Self(elmList.arg)
                ,
                (ElmTypeExpr.ElmArray elmArray) =>
                    "Array " + Self(elmArray.arg)
                ,
                (ElmTypeExpr.ElmDict elmDict) =>
                    "Dict " + Self(elmDict.argKey) + " " + Self(elmDict.argVal)
              );

            string AddParens(string s) => addParenthesisIfMultiple ? $"({s})" : s;

            result = elmTypeExpr.Match(
                (ElmTypeExpr.ElmNamedType elmType) => result
                ,
                (ElmTypeExpr.ElmNamedGenericType elmGenType) => AddParens(result)
                ,
                (ElmTypeExpr.ElmMaybe elmMaybe) => AddParens(result)
                ,
                (ElmTypeExpr.ElmList elmList) => AddParens(result)
                ,
                (ElmTypeExpr.ElmArray elmArray) => AddParens(result)
                ,
                (ElmTypeExpr.ElmDict elmDict) => AddParens(result)
              );

            return result;
        }


        private void GenerateElmJsonDecoder(StringBuilderWrapper sb, ElmTypeDef elmType)
        {
            sb.AppendLine();
            sb.AppendLine();
            elmType.Switch(
                (ElmTypeDef.ElmRecord elmRecord) =>
                {
                    var hasFields = (elmRecord.ElmFields?.Count ?? 0) > 0;
                    sb.AppendLine($"decode{elmRecord.Name} : JD.Decoder {elmRecord.Name}");
                    sb.AppendLine($"decode{elmRecord.Name} =");
                    sb = sb.Indent();
                    sb.AppendLine($"JD.succeed {elmRecord.Name}");
                    if (hasFields)
                    {
                        sb = sb.Indent();
                        foreach (var elmField in elmRecord.ElmFields)
                        {
                            var elmDecoder = ElmDecoder(elmField.ElmType, elmRecord.Name);
                            ElmAttributeDecoder(sb, elmField, elmDecoder);
                        }
                        sb = sb.UnIndent();
                    }
                    sb = sb.UnIndent();
                },
                (ElmTypeDef.ElmUnion elmUnion) =>
                {
                    switch (elmUnion.Kind)
                    {
                        case ElmTypeDef.UnionKind.CSharpEmulatedUnion:
                            sb.AppendLine($"decode{elmUnion.Name} : JD.Decoder {elmUnion.Name}");
                            sb.AppendLine($"decode{elmUnion.Name} =");
                            sb = sb.Indent();
                            sb.AppendLine("let");
                            sb = sb.Indent();
                            var firstEntry = true;
                            foreach (var elmConstructor in elmUnion.Constructors)
                            {
                                if (!firstEntry)
                                    sb.AppendLine();
                                else
                                    firstEntry = false;

                                elmType.ThrowIfNull();
                                sb.AppendLine($"build{elmConstructor.Constructor} record =");
                                sb = sb.Indent();
                                sb.AppendLine($"JD.succeed ({elmConstructor.Constructor} record)");
                                sb = sb.UnIndent();
                                sb.AppendLine();
                                // Notice the appended _ to avoid conflicts between the per type generated decoders
                                // and the internally used decoder for a constructor.
                                sb.AppendLine($"decode{elmConstructor.Constructor}_ =");
                                sb = sb.Indent();
                                ElmTypeExpr constrType = elmConstructor.Contains.AsT2;
                                var decoder = ElmDecoder(constrType, elmUnion.Name);
                                sb.AppendLine(decoder);
                                sb = sb.Indent();
                                sb.AppendLine($"|> JD.andThen build{elmConstructor.Constructor}");
                                sb = sb.UnIndent();
                                sb = sb.UnIndent();
                            }
                            sb = sb.UnIndent();
                            sb.AppendLine("in");
                            sb.AppendLine($"JD.oneOf");
                            sb = sb.Indent();
                            var prefix1 = "[";
                            foreach (var elmConstructor in elmUnion.Constructors)
                            {
                                elmType.ThrowIfNull();
                                sb.AppendLine($"{prefix1} JD.field \"{elmConstructor.JsonKey}\" decode{elmConstructor.Constructor}_");
                                prefix1 = ",";
                            }
                            sb.AppendLine("]");
                            sb = sb.UnIndent();
                            sb = sb.UnIndent();
                            break;
                        case ElmTypeDef.UnionKind.WorkaroundForRecursiveTypeWithOnlyOneConstructor:
                            var constructor = elmUnion.Constructors.First();
                            List<ElmTypeDef.ElmField> elmFields = constructor.Contains.AsT1;
                            elmFields.ThrowIfNull();

                            sb.AppendLine($"decode{elmUnion.Name} : JD.Decoder {elmUnion.Name}");
                            sb.AppendLine($"decode{elmUnion.Name} =");
                            sb = sb.Indent();
                            sb.AppendLine("let");
                            sb = sb.Indent();
                            var builderArguments = elmFields.Map(ef => ef.Label).Join(" ");
                            var suffix = builderArguments + " ";
                            sb.AppendLine($"build{constructor.Constructor} {suffix}=");
                            sb = sb.Indent();
                            sb.AppendLine(constructor.Constructor);
                            sb = sb.Indent();
                            var prefix2 = "{";
                            foreach (var elmField in elmFields)
                            {
                                sb.AppendLine($"{prefix2} {elmField.Label} = {elmField.Label}");
                                prefix2 = ",";
                            }
                            sb.AppendLine("}");
                            sb = sb.UnIndent();
                            sb = sb.UnIndent();
                            sb = sb.UnIndent();
                            sb.AppendLine("in");
                            sb.AppendLine($"JD.succeed build{constructor.Constructor}");
                            sb = sb.Indent();
                            foreach (var elmField in elmFields)
                            {
                                var elmDecoder = ElmDecoder(elmField.ElmType, elmUnion.Name);
                                ElmAttributeDecoder(sb, elmField, elmDecoder);
                            }
                            sb = sb.UnIndent();
                            sb = sb.UnIndent();
                            break;
                        case ElmTypeDef.UnionKind.Enum:
                            sb.AppendLine($"decode{elmUnion.Name} : JD.Decoder {elmUnion.Name}");
                            sb.AppendLine($"decode{elmUnion.Name} =");
                            sb = sb.Indent();
                            sb.AppendLine("let");
                            sb = sb.Indent();
                            sb.AppendLine($"decodeConstructor string =");
                            sb = sb.Indent();
                            sb.AppendLine($"case string of");
                            sb = sb.Indent();
                            foreach (var elmConstructor in elmUnion.Constructors)
                            {
                                sb.AppendLine($"\"{elmConstructor.JsonKey}\" ->");
                                sb = sb.Indent();
                                sb.AppendLine($"JD.succeed {elmConstructor.Constructor}");
                                sb = sb.UnIndent();
                                sb.AppendLine();
                            }
                            sb.AppendLine($"_ ->");
                            sb = sb.Indent();
                            sb.AppendLine($"JD.fail (\"Invalid pattern \" ++ string ++ \" for {elmUnion.Name}\")");
                            sb = sb.UnIndent();
                            sb = sb.UnIndent();
                            sb = sb.UnIndent();
                            sb = sb.UnIndent();
                            sb.AppendLine("in");
                            sb.AppendLine($"JD.string |> JD.andThen decodeConstructor");
                            sb = sb.UnIndent();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                );
        }


        private void GenerateElmJsonEncoder(StringBuilderWrapper sb, ElmTypeDef elmType)
        {
            sb.AppendLine();
            sb.AppendLine();
            elmType.Switch(
                (ElmTypeDef.ElmRecord elmRecord) =>
                {
                    var hasFields = (elmRecord.ElmFields?.Count ?? 0) > 0;
                    var elmRecordVariable = "record";
                    sb.AppendLine($"encode{elmRecord.Name} : {elmRecord.Name} -> JE.Value");
                    sb.AppendLine($"encode{elmRecord.Name} {elmRecordVariable} =");
                    sb = sb.Indent();
                    if (hasFields)
                    {
                        var elmFields = elmRecord.ElmFields;
                        GenerateElmFieldsEncoder(sb, elmFields, elmRecordVariable);
                    }
                    else
                    {
                        sb.AppendLine($"JE.object []");
                    }
                    sb = sb.UnIndent();
                }
                , (ElmTypeDef.ElmUnion elmUnion) =>
                 {
                     switch (elmUnion.Kind)
                     {
                         case ElmTypeDef.UnionKind.CSharpEmulatedUnion:
                             sb.AppendLine($"encode{elmUnion.Name} : {elmUnion.Name} -> JE.Value");
                             sb.AppendLine($"encode{elmUnion.Name} union =");
                             sb = sb.Indent();
                             sb.AppendLine("let");
                             sb = sb.Indent();
                             sb.AppendLine("( jsonKey, encodedValue ) =");
                             sb = sb.Indent();
                             sb.AppendLine("case union of");
                             sb = sb.Indent();
                             var firstEntry = true;
                             foreach (var elmConstructor in elmUnion.Constructors)
                             {
                                 if (!firstEntry)
                                     sb.AppendLine();
                                 else
                                     firstEntry = false;
                                 sb.AppendLine($"{elmConstructor.Constructor} record ->");
                                 sb = sb.Indent();
                                 sb.AppendLine($"( \"{elmConstructor.JsonKey}\"");
                                 ElmTypeExpr elmConstrType = elmConstructor.Contains.AsT2;
                                 elmConstrType.ThrowIfNull();
                                 var encoder = ElmEncoder(elmConstrType, $"record");
                                 sb.AppendLine($", {encoder}");
                                 sb.AppendLine($")");
                                 sb = sb.UnIndent();
                             }
                             sb = sb.UnIndent();
                             sb = sb.UnIndent();
                             sb = sb.UnIndent();
                             sb.AppendLine("in");
                             sb.AppendLine($"JE.object");
                             sb = sb.Indent();
                             sb.AppendLine("[ ( jsonKey, encodedValue )");
                             sb.AppendLine("]");
                             sb = sb.UnIndent();
                             sb = sb.UnIndent();
                             break;
                         case ElmTypeDef.UnionKind.WorkaroundForRecursiveTypeWithOnlyOneConstructor:
                             var constructor = elmUnion.Constructors.First();
                             List<ElmTypeDef.ElmField> elmFields = constructor.Contains.AsT1;
                             var elmRecordVariable = "record";
                             sb.AppendLine($"encode{elmUnion.Name} : {elmUnion.Name} -> JE.Value");
                             sb.AppendLine($"encode{elmUnion.Name} ({constructor.Constructor} {elmRecordVariable}) =");
                             sb = sb.Indent();
                             GenerateElmFieldsEncoder(sb, elmFields, elmRecordVariable);
                             sb = sb.UnIndent();
                             break;
                         case ElmTypeDef.UnionKind.Enum:
                             sb.AppendLine($"encode{elmUnion.Name} : {elmUnion.Name} -> JE.Value");
                             sb.AppendLine($"encode{elmUnion.Name} union =");
                             sb = sb.Indent();
                             sb.AppendLine($"JE.string <|");
                             sb = sb.Indent();
                             sb.AppendLine($"case union of");
                             sb = sb.Indent();
                             firstEntry = true;
                             foreach (var elmConstructor in elmUnion.Constructors)
                             {
                                 if (!firstEntry)
                                     sb.AppendLine();
                                 else
                                     firstEntry = false;
                                 sb.AppendLine($"{elmConstructor.Constructor} ->");
                                 sb = sb.Indent();
                                 sb.AppendLine($"\"{elmConstructor.JsonKey}\"");
                                 sb = sb.UnIndent();
                             }
                             sb = sb.UnIndent();
                             sb = sb.UnIndent();
                             sb = sb.UnIndent();
                             break;
                         default:
                             throw new ArgumentOutOfRangeException();
                     }
                 }
                );
        }


        private void GenerateElmHttpClient(StringBuilderWrapper sb, ElmTypeDef elmType)
        {
            // Every request DTO is always an ElmRecord (by the design of ServiceStack)
            // Hence only process those for return types.
            if (!elmType.IsT0)
                return;

            ElmTypeDef.ElmRecord requestDto = elmType.AsT0;
            var responseDto = requestDto.ReturnType;
            if (responseDto == null)
                return;

            var httpMethodName = $"send{requestDto.Name}";
            var defaultsName = $"{httpMethodName}Defaults";

            // Default in ServiceStack if NO route was provided
            var route = requestDto.HttpRoutes?.First();
            var path = route?.Path ?? "Json?";
            var verb = route?.Verb ?? ElmTypeDef.HttpVerb.Get;

            sb.AppendLine();
            sb.AppendLine();
            var responseTypeExpr = ElmTypeExprToString(requestDto.ReturnType, addParenthesisIfMultiple: true);
            sb.AppendLine($"{httpMethodName} : {requestDto.Name} -> (Result Http.Error {responseTypeExpr} -> msg) -> Cmd msg");
            sb.AppendLine($"{httpMethodName} requestDto msgBuilder =");
            sb = sb.Indent();
            sb.AppendLine($"Http.request <| requestFromDefaults {defaultsName} requestDto msgBuilder");
            sb = sb.UnIndent();

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine($"{defaultsName} : RequestDefaults {requestDto.Name} {responseTypeExpr} msg");
            sb.AppendLine($"{defaultsName} =");
            sb = sb.Indent();
            var method = "";
            var body = "";
            Action createUrlBuilder;
            switch (verb)
            {
                case ElmTypeDef.HttpVerb.Get:
                    method = "GET";
                    body = "Http.emptyBody";
                    createUrlBuilder = () =>
                    {
                        sb.AppendLine($"let");
                        sb = sb.Indent();
                        sb.AppendLine("params =");
                        sb = sb.Indent();
                        if (requestDto.ElmFields.Count == 0)
                            sb.AppendLine("[]");
                        else
                        {
                            var separator = "[";
                            foreach (var elmField in requestDto.ElmFields)
                            {
                                var encoder = ElmEncoder(elmField.ElmType, $"requestDto.{elmField.Label}");
                                sb.AppendLine($"{separator} ( \"{elmField.JsonKey}\", JE.encode 0 <| {encoder} )");
                                separator = ",";
                            };
                            sb.AppendLine("]");
                        }
                        sb = sb.UnIndent();
                        sb = sb.UnIndent();
                        sb.AppendLine("in");
                        sb.AppendLine(@"path ++ ""?"" ++ encodeParams params");
                    };
                    break;
                case ElmTypeDef.HttpVerb.Post:
                    method = "POST";
                    body = $"Http.jsonBody <| encode{requestDto.Name} requestDto";
                    createUrlBuilder = () =>
                    {
                        sb.AppendLine($"path");
                    };
                    break;
                case ElmTypeDef.HttpVerb.Put:
                    method = "PUT";
                    body = $"Http.jsonBody <| encode{requestDto.Name} requestDto";
                    createUrlBuilder = () =>
                    {
                        sb.AppendLine($"path");
                    };
                    break;
                default:
                    throw new Exception($"Unsuppoorted HTTP Verb {verb.ToJson()}");
            }
            sb.AppendLine($"{{ path = \"{path}\"");
            sb.AppendLine($", urlBuilder =");
            sb = sb.Indent();
            // Only GET requests use the dto to add parameters, so avoid capturing it if not required,
            // to avoid lot's of unused variable warnings from elm-analyse
            var requestDtoLambdaQueryArgument = method == "GET" ? "requestDto" : "_";
            sb.AppendLine($"\\path {requestDtoLambdaQueryArgument} ->");
            sb = sb.Indent();
            createUrlBuilder();
            sb = sb.UnIndent();
            sb = sb.UnIndent();
            sb.AppendLine($", method = \"{method}\"");
            // Only non GET requests use the dto to add parameters, so avoid capturing it if not required,
            // to avoid lot's of unused variable warnings from elm-analyse
            var requestDtoLambdaBodyArgument = method == "GET" ? "_" : "requestDto";
            sb.AppendLine($", bodyBuilder = \\{requestDtoLambdaBodyArgument} -> {body}");
            var decoder = ElmDecoder(responseDto, requestDto.Name);
            sb.AppendLine($", expectBuilder = \\msgBuilder -> Http.expectJson msgBuilder {decoder}");
            sb.AppendLine($", timeout = Just {{- milliseconds -}} 30000");
            sb.AppendLine($", tracker = Nothing");
            sb.AppendLine($", headers = [ Http.header \"Accept\" \"application/json\" ]");
            sb.AppendLine("}");
            sb = sb.UnIndent();
        }


        private void GenerateElmFieldsEncoder(StringBuilderWrapper sb, List<ElmTypeDef.ElmField> elmFields, string elmRecordVariable)
        {
            sb.AppendLine($"let");
            sb = sb.Indent();
            var firstEntry = true;
            foreach (var elmField in elmFields)
            {
                if (!firstEntry)
                    sb.AppendLine();
                else
                    firstEntry = false;
                sb.AppendLine($"encoded{elmField.Label} =");
                sb = sb.Indent();
                var encoder = ElmEncoder(elmField.ElmType, $"{elmRecordVariable}.{elmField.Label}");
                sb.AppendLine(encoder);
                sb = sb.UnIndent();
            }
            sb = sb.UnIndent();
            sb.AppendLine($"in");
            sb.AppendLine($"JE.object");
            sb = sb.Indent();
            var prefix = "[";
            foreach (var elmField in elmFields)
            {
                sb.AppendLine($"{prefix} ( \"{elmField.JsonKey}\", encoded{elmField.Label} )");
                prefix = ",";
            }
            sb.AppendLine("]");
            sb = sb.UnIndent();
            sb = sb.UnIndent();
        }


        private static void ElmAttributeDecoder(StringBuilderWrapper sb, ElmTypeDef.ElmField elmField, string elmDecoder)
        {
            void optional() => sb.AppendLine($"|> JDP.optional \"{elmField.JsonKey}\" {elmDecoder} Nothing");
            void required() => sb.AppendLine($"|> JDP.required \"{elmField.JsonKey}\" {elmDecoder}");

            elmField.ElmType.Switch(
              (ElmTypeExpr.ElmNamedType elmType) => required(),
              (ElmTypeExpr.ElmNamedGenericType elmGenType) => required(),
              (ElmTypeExpr.ElmMaybe elmMaybe) => optional(),
              (ElmTypeExpr.ElmList elmList) => required(),
              (ElmTypeExpr.ElmArray elmArray) => required(),
              (ElmTypeExpr.ElmDict elmDict) => required()
              );
        }


        private string ElmDecoder(ElmTypeExpr elmType, string parentTypeName)
        {
            string LookupDecoder(string name)
            {
                ElmDecoders.TryGetValue(name, out string functionName);
                if (functionName == null)
                    functionName = $"decode{name}";
                if (parentTypeName == name)
                    functionName = $"(JD.lazy (\\_ -> {functionName}))";
                return functionName;
            }
            string parens(string codeSofar) => $"({codeSofar})";

            string Self(ElmTypeExpr et) => ElmDecoder(et, parentTypeName);

            var decoder = elmType.Match(
                (ElmTypeExpr.ElmNamedType elmNamed) => LookupDecoder(elmNamed.ElmName),
                (ElmTypeExpr.ElmNamedGenericType elmNamedGeneric) => throw new Exception("Unsupported"),
                (ElmTypeExpr.ElmMaybe elmMaybe) => parens("JD.nullable " + Self(elmMaybe.arg)),
                (ElmTypeExpr.ElmList elmList) => parens("JD.list " + Self(elmList.arg)),
                (ElmTypeExpr.ElmArray elmArray) => parens("JD.array " + Self(elmArray.arg)),
                (ElmTypeExpr.ElmDict elmDict) => parens("JDE.dict2 " + Self(elmDict.argKey) + " " + Self(elmDict.argVal))
              );

            return decoder;
        }

        // Take a list of types and generate encodings with the patterns:
        // (JE.list (\x -> encodeOption x) record.options)
        private string ElmEncoder(ElmTypeExpr elmType, string literalElmParam, bool addParenthesisIfMultiple = false)
        {
            string LookupEncoder(string name)
            {
                ElmEncoders.TryGetValue(name, out string encoder_);
                if (encoder_ == null)
                    encoder_ = $"encode{name}";
                return encoder_;
            }
            string LookupToString(ElmTypeExpr elmType_)
            {
                string result = null;
                var errorExplanation = "ELM Dict only supports a hardcoded set of simple key types";
                void error() => throw new Exception($"Could not find required string encoder for {ElmTypeExprToString(elmType_)} {errorExplanation}");
                elmType_.Switch(
                    (ElmTypeExpr.ElmNamedType elmNamed) =>
                    {
                        ElmToString.TryGetValue(elmNamed.ElmName, out result);
                        if (result == null)
                            error();
                    },
                    (ElmTypeExpr.ElmNamedGenericType elmNamedGeneric) => error(),
                    (ElmTypeExpr.ElmMaybe elmMaybe) => error(),
                    (ElmTypeExpr.ElmList elmList) => error(),
                    (ElmTypeExpr.ElmArray elmArray) => error(),
                    (ElmTypeExpr.ElmDict elmDict) => error()
                );
                return result;
            }
            string maybeLiteral(string encoderSoFar)
                => literalElmParam != null ? $"{encoderSoFar} {literalElmParam}" : encoderSoFar;
            string maybeParens(string encoderSoFar)
                => addParenthesisIfMultiple ? $"({encoderSoFar})" : encoderSoFar;

            string Self(ElmTypeExpr et) => ElmEncoder(et
                , null // Only the top level invocation use the literal elm parameter.
                , true // Only top level can skip parentheses.
                );

            var encoder =
                elmType.Match(
                (ElmTypeExpr.ElmNamedType elmNamed) => maybeLiteral($"{LookupEncoder(elmNamed.ElmName)}"),
                (ElmTypeExpr.ElmNamedGenericType elmNamedGeneric) => throw new Exception("Unsupported"),
                (ElmTypeExpr.ElmMaybe elmMaybe) => maybeParens(maybeLiteral($"JEE.maybe {Self(elmMaybe.arg)}")),
                (ElmTypeExpr.ElmList elmList) => maybeParens(maybeLiteral($"JE.list {Self(elmList.arg)}")),
                (ElmTypeExpr.ElmArray elmArray) => maybeParens(maybeLiteral($"JE.array {Self(elmArray.arg)}")),
                (ElmTypeExpr.ElmDict elmDict) => maybeParens(maybeLiteral($"JE.dict {LookupToString(elmDict.argKey)} {Self(elmDict.argVal)}"))
            );

            return encoder;
        }


        // TODO Combine C# Stream with Elm File upload/download.
        private static bool ReferencesStream(MetadataTypes metadata)
        {
            return metadata.GetAllMetadataTypes().Any(x => x.Name == "Stream" && x.Namespace == "System.IO");
        }

        //Use built-in types already in net.servicestack.client package
        public static HashSet<string> IgnoreTypeNames = new HashSet<string>
        {
            typeof(ResponseStatus).Name,
            typeof(ResponseError).Name,
            typeof(ErrorResponse).Name,
        };

        private void RemoveIgnoredTypes(MetadataTypes metadata)
        {
            metadata.RemoveIgnoredTypes(Config);
            metadata.Types.RemoveAll(x => IgnoreTypeNames.Contains(x.Name));
        }

        public static HashSet<string> ListTypes = new HashSet<string>
        {
            "List`1",
            "IEnumerable`1",
            "ICollection`1",
            "HashSet`1",
            "Queue`1",
            "Stack`1",
            "IEnumerable",
        };

        public static HashSet<string> DictionaryTypes = new HashSet<string>
        {
            "Dictionary`2",
            "IDictionary`2",
            "IOrderedDictionary`2",
            "OrderedDictionary",
            "StringDictionary",
            "IDictionary",
            "IOrderedDictionary",
        };

        public ElmTypeExpr ElmTypeExpression(string type, string[] genericArgs, List<MetadataAttribute> attributes)
        {
            var isMaybe = attributes != null ? attributes.Any(a => a.Name == MaybeAttributeName) : false;
            if (isMaybe)
            {
                return new ElmTypeExpr.ElmMaybe
                {
                    arg = ElmTypeExpression(type, genericArgs, attributes: null /* The maybe attribute is already handled */)
                };
            }

            if (genericArgs != null)
            {
                if (type == "Nullable`1")
                    return new ElmTypeExpr.ElmMaybe
                    {
                        arg = GenericArg(genericArgs[0])
                    };
                if (ListTypes.Contains(type))
                {
                    return new ElmTypeExpr.ElmList
                    {
                        arg = GenericArg(genericArgs[0])
                    };
                }
                if (DictionaryTypes.Contains(type))
                {
                    return new ElmTypeExpr.ElmDict
                    {
                        argKey = GenericArg(genericArgs[0]),
                        argVal = GenericArg(genericArgs[1])
                    };
                }

                var parts = type.Split('`');
                if (parts.Length > 1)
                {
                    var typeName = MapCSharpToElm(type);

                    ElmTypeExpr.ElmNamedType namedType = typeName.AsT0;
                    if (namedType == null)
                        throw new Exception("Logic error - only named types can have generic arguments.");
                    return new ElmTypeExpr.ElmNamedGenericType
                    {
                        ElmName = namedType.ElmName,
                        args = genericArgs.Select(GenericArg).ToList()
                    };
                }
            }
            else
            {
                type = type.StripNullable();
            }

            return MapCSharpToElm(type);
        }

        private ElmTypeExpr MapCSharpToElm(string type)
        {
            type = type.SanitizeType();
            var arrParts = type.SplitOnFirst('[');
            if (arrParts.Length > 1)
                return new ElmTypeExpr.ElmArray
                {
                    arg = GenericArg(arrParts[0])
                };

            string typeAlias;
            CSharpToElmMapping.TryGetValue(type, out typeAlias);
            var result = new ElmTypeExpr.ElmNamedType
            {
                ElmName = typeAlias ?? NameOnly(type)
            };

            return result;
        }

        public ElmTypeExpr GenericArg(string arg)
        {
            var nodes = arg.TrimStart('\'').ParseTypeIntoNodes();
            return ConvertFromCSharp(nodes);
        }

        public ElmTypeExpr ConvertFromCSharp(TextNode node)
        {
            var sb = new StringBuilder();
            ElmTypeExpr result;

            if (node.Text == "List")
            {
                result = new ElmTypeExpr.ElmList
                {
                    arg = ConvertFromCSharp(node.Children[0])
                };
            }
            else if (node.Text == "Dictionary")
            {
                result = new ElmTypeExpr.ElmDict
                {
                    argKey = ConvertFromCSharp(node.Children[0]),
                    argVal = ConvertFromCSharp(node.Children[1]),
                };
            }
            else
            {
                result = MapCSharpToElm(node.Text);
                if (node.Children.Any())
                {
                    ElmTypeExpr.ElmNamedType namedType = result.AsT0;
                    if (namedType == null)
                        throw new Exception("Logic error - only named types can have generic arguments.");
                    result = new ElmTypeExpr.ElmNamedGenericType
                    {
                        ElmName = namedType.ElmName,
                        args = node.Children.Select(tn => GenericArg(tn.Text)).ToList()
                    };
                }
            }

            return result;
            // TODO: Consider how removal of nested classes should be done.
            // was previously 
            //    var typeName = sb.ToString();
            //    return typeName.LastRightPart('.'); //remove nested class
        }


        public string NameOnly(string type)
        {
            var name = conflictTypeNames.Contains(type)
                ? type.Replace('`', '_')
                : type.SplitOnFirst('`')[0];

            return name.LastRightPart('.').SafeToken();
        }

        public bool AppendComments(StringBuilderWrapper sb, string desc)
        {
            if (desc != null && Config.AddDescriptionAsComments)
            {
                sb.AppendLine("{-");
                sb.AppendLine("* {0}".Fmt(desc.SafeComment()));
                sb.AppendLine("-}");
            }
            return false;
        }

    }


    public static class ElmGeneratorExtensions
    {
        public static string InheritedType(this string type)
        {
            return type;
        }

        // Ref.https://github.com/elm-lang/elm-compiler/blob/7ee7742a16188df7ff498ec4ef9f8b49e58a35fe/src/Parse/Primitives.hs#L364-L375
        // + infix*
        public static HashSet<string> ElmKeyWords = new HashSet<string>
        {
            "if",
            "then",
            "else",
            "case",
            "of",
            "let",
            "in",
            "type",
            "module",
            "where",
            "import",
            "exposing",
            "as",
            "port",
            "infix",
            "infixl",
            "infixr",
        };

        public static bool IsKeyWord(this string name)
        {
            return ElmKeyWords.Contains(name);
        }

        public static string PropertyStyle(this string name)
        {
            //Gson is case-sensitive, fieldName needs to match json
            var fieldName = JsConfig.TextCase == TextCase.CamelCase
                ? name.ToCamelCase()
                : JsConfig.TextCase == TextCase.SnakeCase
                ? name.ToLowercaseUnderscore()
                : name;

            return fieldName;
        }


        // Copied from https://github.com/ServiceStack/ServiceStack/blob/master/src/ServiceStack/NativeTypes/NativeTypesMetadata.cs#L1245 and may be removed on merge upstream.
        //Workaround to handle Nullable<T>[] arrays. Most languages don't support Nullable in nested types
        internal static string StripNullable(this string type)
        {
            return ServiceStack.NativeTypes.MetadataExtensions.StripGenericType(type, "Nullable");
        }
    }
}