@echo off
rem uwcheck: our port checked headless by the Underworld Editor's Exhumed check. See tools\uwcheck\README.md.
rem Run through dotnet.exe: Defender's ASR rule blocks a new unsigned exe.
set DOTNET_CLI_HOME=O:\appsnotonc\dotnet10\home
"O:\appsnotonc\dotnet10\dotnet.exe" "%~dp0..\..\work\uwcheck\bin\Release\net10.0\uwcheck.dll" %*
