@echo off
if [%*]==[] %0 "C:\Data\Source\DevStudio\Cegs12X\bin\Release"
C:
CD "\Programs\Aeon Laboratories\Cegs12X"
copy "%*\*.exe" > nul
copy "%*\*.dll" > nul
copy "%*\*.config" > nul
copy "%*\*.deps.json" > nul
copy "%*\*.runtimeconfig.json" > nul
echo *** System software updated *** >> "log\Event log.txt"
exit
