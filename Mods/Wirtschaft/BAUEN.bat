@echo off
setlocal
rem Baut Wirtschaft.dll mit dem C#-Compiler, der in Windows schon dabei ist.
rem Einfach doppelklicken. Danach liegt Wirtschaft.dll neben ModInfo.xml.

set "SPIEL=C:\Program Files (x86)\Spiele\steamapps\common\7 Days To Die"
if not "%~1"=="" set "SPIEL=%~1"
set "M=%SPIEL%\7DaysToDie_Data\Managed"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "HIER=%~dp0"

if not exist "%CSC%" goto keincompiler
if not exist "%M%\Assembly-CSharp.dll" goto keinspiel

echo Baue Wirtschaft.dll ...
"%CSC%" /nologo /noconfig /nostdlib+ /target:library /optimize+ /codepage:65001 /warn:1 /out:"%HIER%Wirtschaft.dll" /r:"%M%\mscorlib.dll" /r:"%M%\System.dll" /r:"%M%\System.Core.dll" /r:"%M%\System.Xml.dll" /r:"%M%\netstandard.dll" /r:"%M%\Assembly-CSharp.dll" /r:"%M%\LogLibrary.dll" /r:"%M%\UnityEngine.CoreModule.dll" /recurse:"%HIER%Quellcode\*.cs" > "%HIER%Baufehler.txt" 2>&1
if errorlevel 1 goto fehler

del "%HIER%Baufehler.txt" >nul 2>&1
echo.
echo FERTIG: Wirtschaft.dll wurde erstellt. Du kannst das Spiel starten.
goto ende

:fehler
echo.
echo FEHLGESCHLAGEN. Die Meldungen stehen in Baufehler.txt:
echo.
type "%HIER%Baufehler.txt"
goto ende

:keincompiler
echo FEHLER: Windows-Compiler csc.exe nicht gefunden.
goto ende

:keinspiel
echo FEHLER: Spielordner nicht gefunden. Pfad oben in BAUEN.bat bei SPIEL anpassen.
goto ende

:ende
echo.
pause
