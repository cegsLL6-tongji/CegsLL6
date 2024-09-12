@echo off
if [%*]==[] %0 "C:\Data\Source\DevStudio\CegsLL6\bin\Release"
C:
CD "\Programs\Aeon Laboratories\CegsLL6"
copy "%*\*.exe" > nul
copy "%*\*.dll" > nul
copy "%*\*.pdb" > nul
copy "%*\*.deps.json" > nul
copy "%*\*.runtimeconfig.json" > nul
echo *** System software updated *** >> "log\Event log.txt"
exit
