# CloneLeeroy

CloneLeeroy .NET tool.

[![Build](https://github.com/bgrainger/CloneLeeroy/workflows/Build/badge.svg)](https://github.com/bgrainger/CloneLeeroy/actions?query=workflow%3ABuild) [![NuGet](https://img.shields.io/nuget/v/CloneLeeroy.svg)](https://www.nuget.org/packages/CloneLeeroy)

* [Release Notes](ReleaseNotes.md)

## Suggestions

CloneLeeroy supports tab completion on the command line. To enable it, follow [the steps here](https://github.com/dotnet/command-line-api/blob/main/docs/dotnet-suggest.md) to install `dotnet-suggest` and add a script to your shell profile.

### Debugging

To test suggestions while developing the application, create `%USERPROFILE%\.dotnet-suggest-registration.txt` containing the line `C:\Full\Path\To\bin\Debug\net5.0\CloneLeeroy.exe`. Then restart your shell.

Now running `.\CloneLeeroy.exe <TAB>` will provide suggestions.
