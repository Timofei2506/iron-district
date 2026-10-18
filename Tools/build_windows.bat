@echo off
rem Local Windows build. Point UNITY_PATH to your Unity 2022.3 editor if it's not in the default Hub folder.
setlocal
if "%UNITY_PATH%"=="" set UNITY_PATH=%ProgramFiles%\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe
if not exist "%UNITY_PATH%" (
  echo [!] Unity not found at: %UNITY_PATH%
  echo     Set UNITY_PATH, e.g.:  set UNITY_PATH=C:\Unity\2022.3.22f1\Editor\Unity.exe
  pause
  exit /b 1
)
echo Building Iron District for Windows...
"%UNITY_PATH%" -batchmode -nographics -quit ^
  -projectPath "%~dp0.." ^
  -executeMethod IronDistrict.EditorTools.CiBuild.BuildWindows ^
  -logFile "%~dp0build_windows.log"
if errorlevel 1 (
  echo Build FAILED - see Tools\build_windows.log
) else (
  echo Done: build\StandaloneWindows64\IronDistrict.exe
  explorer "%~dp0..\build\StandaloneWindows64"
)
pause
