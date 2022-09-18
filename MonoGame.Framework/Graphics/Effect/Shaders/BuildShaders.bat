@echo off
setlocal

SET MGFXC="..\..\..\..\Tools\MonoGame.Effect.Compiler\bin\Windows\AnyCPU\Release\2mgfx.exe"

@for /f %%f IN ('dir /b *.fx') do (

  echo %%~nf.fx
  
  call %MGFXC% %%~nf.fx ..\Resources\%%~nf.dx11.mgfxo /Platform:Windows

  call %MGFXC% %%~nf.fx ..\Resources\%%~nf.ogl.mgfxo /Platform:Android

)

endlocal
pause
