@echo off
setlocal
cd /d "%~dp0"
if not exist "node_modules\vite\bin\vite.js" (
  call npm.cmd ci
  if errorlevel 1 exit /b 1
)
if not exist "public\media\rooms\university.glb" (
  echo Aset WebXR belum ada. Lihat langkah ekspor di README.md.
  exit /b 1
)
call npm.cmd run dev
