@echo off
setlocal
set "TOOL_DIR=%~dp0"
for %%I in ("%TOOL_DIR%..\..") do set "PROJECT_ROOT=%%~fI"

py -3 "%TOOL_DIR%batch_convert.py" ^
  --input-dir "%PROJECT_ROOT%\asset_work\character_sources" ^
  --output-dir "%PROJECT_ROOT%\asset_work\character_pixelized" ^
  --report-dir "%PROJECT_ROOT%\asset_work\character_pixel_reports" ^
  --contact-sheet-dir "%PROJECT_ROOT%\asset_work\character_contact_sheets" ^
   --preset website_master ^
  --limit 8 ^
  --overwrite ^
  --workers 4 ^
  --verbose

exit /b %ERRORLEVEL%
