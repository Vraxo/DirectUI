@echo off
REM Runs the Fuse tool for the Cosmocrush project

echo Starting Fuse process for Cosmocrush...
echo Outputting to: D:\Parsa Stuff\Godot\Cosmocrush\MergedCodeForAI.txt
echo Scanning folders:
echo   - D:\Parsa Stuff\Visual Studio\DirectUI\DirectUI\Source
echo ==================================================

Fuse.exe "D:\Parsa Stuff\Visual Studio\DirectUI\MergedCodeForAI.txt" "D:\Parsa Stuff\Visual Studio\DirectUI\DirectUI\Source" "D:\Parsa Stuff\Visual Studio\DirectUI\Cocoshell"

echo ==================================================
echo Fuse process finished. Press any key to close this window.
pause