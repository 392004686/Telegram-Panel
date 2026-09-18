@echo off
setlocal
cd /d "%~dp0"

where node >nul 2>nul || goto :missing_node
where corepack >nul 2>nul || goto :missing_corepack

echo [1/3] 准备 pnpm 10.20.0...
call corepack install --global pnpm@10.20.0 || goto :failed
echo [2/3] 检查前端依赖...
call pnpm --dir frontend install || goto :failed
echo [3/3] 启动 UI 预览...
set VITE_UI_PREVIEW=true
set VITE_PANEL_API_TARGET=http://127.0.0.1:5000
echo.
echo UI 预览地址: http://localhost:5173/ui/dashboard
echo 预览模式已跳过登录，不连接真实登录流程。
echo 关闭此窗口即可停止预览。
echo.
call pnpm --dir frontend dev
goto :end

:missing_node
echo [错误] 未找到 Node.js，请先安装 Node.js 20 或更高版本。
goto :pause
:missing_corepack
echo [错误] 未找到 Corepack，请确认 Node.js 安装完整。
goto :pause
:failed
echo [错误] 预览启动失败。
:pause
pause
:end
endlocal
