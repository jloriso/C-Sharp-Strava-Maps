@echo off
rem Rebuilds the TypeScript pages, then builds and runs GpxWorldMap (opens the browser).
rem Runs from this folder, so gpxworldmap.config.json here is used by default.
rem Any arguments are passed to the app, e.g.:  run.bat --config other.json
setlocal
pushd "%~dp0Web.App"

if not exist "node_modules\.package-lock.json" (
    echo Installing npm packages...
    call npm ci || goto :fail
)

echo Compiling TypeScript pages...
call npm run build || goto :fail

popd
echo Building and running GpxWorldMap...
rem Run from the repo root so gpxworldmap.config.json (and relative paths in it) resolve.
pushd "%~dp0"
dotnet run --project Web.App -p:SkipClientBuild=true -- %*
set "EXITCODE=%ERRORLEVEL%"
popd
endlocal & exit /b %EXITCODE%

:fail
set "EXITCODE=%ERRORLEVEL%"
popd
echo Build failed.
endlocal & exit /b %EXITCODE%
