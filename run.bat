@echo off
rem Builds and runs GpxWorldMap; client pages rebuild only when their inputs change.
rem Runs from this folder so gpxworldmap.config.json and relative paths resolve.
rem Any arguments are passed to the app, e.g.: run.bat --config other.json
setlocal
pushd "%~dp0"
dotnet run --project Web.App -- %*
set "EXITCODE=%ERRORLEVEL%"
popd
endlocal & exit /b %EXITCODE%
