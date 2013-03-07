mkdir "%LOCALAPPDATA%\JetBrains\dotCover\v2.0\Plugins" 2> NUL
mkdir "%LOCALAPPDATA%\JetBrains\dotCover\v2.0\Plugins\mspec" 2> NUL
copy /y Machine.Specifications.dll "%LOCALAPPDATA%\JetBrains\dotCover\v2.0\Plugins\mspec"
copy /y Machine.Specifications.pdb "%LOCALAPPDATA%\JetBrains\dotCover\v2.0\Plugins\mspec" > NUL
copy /y Machine.Specifications.dotCoverRunner.2.0.dll "%LOCALAPPDATA%\JetBrains\dotCover\v2.0\Plugins\mspec"
copy /y Machine.Specifications.dotCoverRunner.2.0.pdb "%LOCALAPPDATA%\JetBrains\dotCover\v2.0\Plugins\mspec" > NUL
pause
