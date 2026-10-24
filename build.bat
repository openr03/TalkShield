@echo off
REM TalkShield 빌드 - Windows 내장 .NET 컴파일러 사용 (추가 설치 불필요)
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

REM 아이콘이 없으면 생성
if not exist "on.ico" powershell -NoProfile -File "make_icons.ps1"

echo [*] 컴파일 중...
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /out:TalkShield.exe ^
  /win32icon:on.ico ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:Microsoft.CSharp.dll ^
  TalkShield.cs

if errorlevel 1 ( echo [!] 빌드 실패 & exit /b 1 )
echo [*] 완료: TalkShield.exe
endlocal
