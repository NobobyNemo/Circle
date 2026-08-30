@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
mkdir "%TEMP%\wasapi_test" 2>nul
cl /nologo /EHsc /Fe:"%TEMP%\wasapi_test\test.exe" /Fo:"%TEMP%\wasapi_test\" "c:\Users\win\Desktop\circle\csharp\AudioTest\native_test.cpp" ole32.lib
