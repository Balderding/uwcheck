@echo off
rem Builds uwcheck into work\uwcheck\bin (the .NET 10 SDK on O:, nothing on C:).
set DOTNET_CLI_HOME=O:\appsnotonc\dotnet10\home
set NUGET_PACKAGES=O:\appsnotonc\dotnet10\home\.nuget\packages
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_NOLOGO=1
"O:\appsnotonc\dotnet10\dotnet.exe" build "%~dp0uwcheck.csproj" -c Release -nologo -v q
