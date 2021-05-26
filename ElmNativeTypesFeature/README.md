# ServiceStack.NativeTypes.Elm

A ServiceStack plugin to generate Elm code API interaction.

## Installation

```
dotnet nuget add ServiceStack.NativeTypes.Elm
```

## Configuration

Edit the `Configure` method `AppHost.cs` and add

```
Plugins.Add(new ElmNativeTypesFeature { });
```

## Usage

Access `/types/elm` to get the generated Elm code.

To make the generated file, use the `elm-ref` tool, with
a path, such as `elm-ref http://localhost:5000/types/elm Dtos.elm`
that will create the file `Dtos.elm`.

For later updates run `elm-ref Dtos.elm` that will use the url stored in the file
and update the `Dtos.elm` file.

## About ServiceStack

For general information about ServiceStack see https://docs.servicestack.net/.
For an introduction to ServiceStack Plugins see https://docs.servicestack.net/plugins.

## Special Elm related features of the code generator

Writes encoders and decoders.
Supports `xUnion` translations into Elm custom types.
Has a custom `[Maybe]` attribute for optional types.
Elm requires unique constructors in the namespace.

## Examples

### Simple dto

### Union dto

### C# naming conventions to avoid Elm constructor conflicts
