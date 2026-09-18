@echo off
setlocal
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 goto missing_node
where corepack >nul 2>nul
if errorlevel 1 goto missing_corepack

echo [1/3] Preparing pnpm 10.20.0...
call corepack install --global pnpm@10.20.0
if errorlevel 1 goto failed

echo [2/3] Installing frontend dependencies...
call corepack pnpm --dir frontend install
if errorlevel 1 goto failed

echo [3/3] Starting UI preview...
set VITE_UI_PREVIEW=true
set VITE_PANEL_API_TARGET=http://127.0.0.1:5000

echo.
echo UI preview: http://localhost:5173/ui/dashboard
echo Login is bypassed in preview mode.
echo Close this window to stop the preview.
echo.
call corepack pnpm --dir frontend dev
goto done

:missing_node
echo ERROR: Node.js was not found.
echo Install Node.js 20 or newer, then run this file again.
goto pause_and_exit

:missing_corepack
echo ERROR: Corepack was not found.
echo Please reinstall Node.js with Corepack enabled.
goto pause_and_exit

:failed
echo ERROR: Failed to start UI preview.

:pause_and_exit
pause

:done
endlocal
