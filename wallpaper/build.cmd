@echo off
rem Builds SeaWandererWallpaper.exe with the C# compiler that ships with Windows (.NET Framework 4).
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
cd /d "%~dp0"
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /out:SeaWandererWallpaper.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll /r:"%FW%\Accessibility.dll" ^
  SeaWandererWallpaper.cs
