@echo off
setlocal
if "%~1"=="" (
    echo Usage: %~nx0 "C:\path\to\KSP_x64_Data\Managed" 1>&2
    exit /b 2
)

set "managed=%~f1"
set "project=%~dp0"
set "compiler=csc.exe"
if defined VSINSTALLDIR if exist "%VSINSTALLDIR%MSBuild\Current\Bin\Roslyn\csc.exe" (
    set "compiler=%VSINSTALLDIR%MSBuild\Current\Bin\Roslyn\csc.exe"
)
if "%compiler%"=="csc.exe" (
    where csc.exe >nul 2>&1
    if errorlevel 1 (
        echo Open Developer Command Prompt for Visual Studio with .NET desktop build tools installed. 1>&2
        exit /b 1
    )
)

for %%A in (mscorlib System Assembly-CSharp UnityEngine.CoreModule UnityEngine.IMGUIModule UnityEngine.PhysicsModule UnityEngine.UI) do (
    if not exist "%managed%\%%A.dll" (
        echo Missing %%A.dll in "%managed%". 1>&2
        exit /b 1
    )
)

set "plugins=%project%GameData\ProjectSundial\Plugins"
if not exist "%plugins%\" (
    mkdir "%plugins%" || exit /b 1
)

"%compiler%" /nologo /noconfig /nostdlib+ /langversion:6 /target:library /optimize+ ^
    "/reference:%managed%\mscorlib.dll" ^
    "/reference:%managed%\System.dll" ^
    "/reference:%managed%\Assembly-CSharp.dll" ^
    "/reference:%managed%\UnityEngine.CoreModule.dll" ^
    "/reference:%managed%\UnityEngine.IMGUIModule.dll" ^
    "/reference:%managed%\UnityEngine.PhysicsModule.dll" ^
    "/reference:%managed%\UnityEngine.UI.dll" ^
    "/out:%plugins%\ProjectSundial.dll" "%project%Source\*.cs"
if errorlevel 1 exit /b 1
echo Built "%plugins%\ProjectSundial.dll"
