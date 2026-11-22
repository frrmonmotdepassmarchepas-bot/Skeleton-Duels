@echo off
cd /d "%~dp0"
if exist "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /out:jeu.exe *.cs
) else (
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" /target:winexe /out:jeu.exe *.cs
)
pause
