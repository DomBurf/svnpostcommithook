@echo off
cls

echo Are you sure you want to continue?
echo.
echo you can press 'CTRL-C' now to quit or any other key to continue.
echo.

pause
echo.
echo Building....

"%BUILD_TOOLS%\nant\nant.exe" -buildfile:FullBuild.build.xml -D:verbose=true FinaliseBuild  > FullBuild.build.log

start notepad FullBuild.build.log

