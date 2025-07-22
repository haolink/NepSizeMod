@echo off

cd "%dp0"

set SZIP="C:\Program Files\7-Zip\7z.exe"

echo Copying files
copy /y ..\NepSizeGMRE\bin\Release\net6.0\NepSizeGMREMerged.dll .\mod_gmre\BepInEx\plugins\NepSizeGMRE.dll >NUL
copy /y ..\NepSizeGMRE\bin\Release\net6.0\NepSizeGMREMerged.dll .\mod_gmre_complete\BepInEx\plugins\NepSizeGMRE.dll >NUL

copy /y ..\NepSizeNepRiders\bin\Release\net6.0\NepSizeNepRidersMerged.dll .\mod_nprd\BepInEx\plugins\NepSize.dll >NUL
copy /y ..\NepSizeNepRiders\bin\Release\net6.0\NepSizeNepRidersMerged.dll .\mod_nprd_complete\BepInEx\plugins\NepSize.dll >NUL

copy /y ..\NepSizeSVSMono\bin\Release\netstandard2.0\NepSizeSVSMonoMerged.dll .\mod_svs\BepInEx\plugins\NepSize.dll >NUL

echo Zipping
for /d %%f in (*.*) do call :zipit %%f
goto end

:zipit

cd %1
if exist ..\%1.zip del ..\%1.zip
echo Packing %1.zip
%SZIP% -bb0 -y a -mx9 ..\%1.zip * >NUL
cd ..

:end