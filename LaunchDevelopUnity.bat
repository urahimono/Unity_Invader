@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Optional override: set UNITY_EDITOR_PATH to the full path of Unity.exe.
rem Example: set "UNITY_EDITOR_PATH=D:\Unity Editors\6000.6.4f1\Editor\Unity.exe"

pushd "%~dp0"
if errorlevel 1 goto root_error
set "repoRoot=%CD%"
set "projectPath=%repoRoot%\Invader"
set "statusFile="

echo [INFO] Repository: "%repoRoot%"
git --version
if errorlevel 1 goto git_error
git rev-parse --show-toplevel
if errorlevel 1 goto git_error
echo [INFO] Current branch:
git branch --show-current
if errorlevel 1 goto git_error

rem Capture status separately so a Git failure cannot look like a clean tree.
set "statusFile=%TEMP%\LaunchDevelopUnity-%RANDOM%-%RANDOM%.tmp"
git status --porcelain=v1 --untracked-files=all > "%statusFile%"
if errorlevel 1 goto git_error
for %%F in ("%statusFile%") do if %%~zF GTR 0 goto dirty_error
del "%statusFile%"
set "statusFile="

echo [INFO] Switching to develop...
git switch develop
if errorlevel 1 goto git_error
echo [INFO] Updating develop from origin with fast-forward only...
git pull --ff-only origin develop
if errorlevel 1 goto git_error
echo [INFO] Updated branch and commit:
git branch --show-current
if errorlevel 1 goto git_error
git log -1 --oneline
if errorlevel 1 goto git_error

set "unityVersion="
if not exist "%projectPath%\ProjectSettings\ProjectVersion.txt" goto version_error
for /f "usebackq tokens=1,2" %%A in ("%projectPath%\ProjectSettings\ProjectVersion.txt") do if "%%A"=="m_EditorVersion:" set "unityVersion=%%B"
if not defined unityVersion goto version_error
echo [INFO] Required Unity version: %unityVersion%
set "unityExe=C:\Program Files\Unity\Hub\Editor\%unityVersion%\Editor\Unity.exe"
if defined UNITY_EDITOR_PATH set "unityExe=%UNITY_EDITOR_PATH%"
if not exist "%unityExe%" goto unity_error

echo [INFO] Starting Unity: "%unityExe%"
echo [INFO] Project: "%projectPath%"
start "" "%unityExe%" -projectPath "%projectPath%"
if errorlevel 1 goto launch_error
popd
endlocal
exit /b 0

:dirty_error
echo [ERROR] Uncommitted changes or untracked files found. No branch switch or pull was performed.
type "%statusFile%"
echo Commit or move these files before retrying. Ignored files are excluded.
goto fail

:git_error
echo [ERROR] Git failed. Unity will not be started. See the Git error above.
echo Check Git availability, repository state, origin access and branch history.
goto fail

:version_error
echo [ERROR] Cannot read m_EditorVersion from "%projectPath%\ProjectSettings\ProjectVersion.txt".
goto fail

:unity_error
echo [ERROR] Unity %unityVersion% was not found at "%unityExe%".
echo Install this version via Unity Hub, or set UNITY_EDITOR_PATH to its full Unity.exe path.
echo Example: set "UNITY_EDITOR_PATH=D:\Unity Editors\%unityVersion%\Editor\Unity.exe"
goto fail

:launch_error
echo [ERROR] Windows could not start Unity. Check the executable path and permissions.
goto fail

:fail
if defined statusFile if exist "%statusFile%" del "%statusFile%"
popd
echo [ERROR] Stopped. No files were discarded or stashed automatically.
pause
endlocal
exit /b 1

:root_error
echo [ERROR] Cannot access the directory containing this batch file.
pause
endlocal
exit /b 1
