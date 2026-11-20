@echo off
setlocal
rem Erzeugt die deutschen Sprachdateien fuer die festen Dialoge mit Piper (offline).
rem Vorher: Spiel einmal starten (dann schreibt die Mod die Liste der Saetze nach Stimmen\*.jsonl).
set "S=%~dp0Stimmen"
set "PIPER=%S%\piper\piper.exe"
set "MANN=%S%\de_DE-thorsten-medium.onnx"
set "FRAU=%S%\de_DE-kerstin-low.onnx"

if not exist "%PIPER%" if exist "%S%\piper_windows_amd64.zip" (
  echo Entpacke Piper ...
  powershell -NoProfile -Command "Expand-Archive -Force -LiteralPath '%S%\piper_windows_amd64.zip' -DestinationPath '%S%'"
)
if not exist "%PIPER%" goto keinpiper
if not exist "%MANN%" goto keinestimme

if exist "%S%\maenner.jsonl" (
  for %%A in ("%S%\maenner.jsonl") do if %%~zA GTR 0 (
    echo Erzeuge Maennerstimmen ...
    "%PIPER%" --model "%MANN%" --output_dir "%S%" --json-input < "%S%\maenner.jsonl"
  )
)
if exist "%FRAU%" if exist "%S%\frauen.jsonl" (
  for %%A in ("%S%\frauen.jsonl") do if %%~zA GTR 0 (
    echo Erzeuge Frauenstimmen ...
    "%PIPER%" --model "%FRAU%" --output_dir "%S%" --json-input < "%S%\frauen.jsonl"
  )
)
echo.
echo FERTIG. Die Stimmen gelten sofort im Spiel (kein Neustart noetig).
goto ende

:keinpiper
echo FEHLER: Piper fehlt. Erwartet: %PIPER%
echo piper_windows_amd64.zip nach Stimmen entpacken, sodass Stimmen\piper\piper.exe existiert.
goto ende

:keinestimme
echo FEHLER: Stimme fehlt: %MANN%
echo de_DE-thorsten-medium.onnx und .onnx.json in den Ordner Stimmen legen.
goto ende

:ende
echo.
pause
