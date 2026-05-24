@for /f "usebackq tokens=*" %%i in (`src\Tools\VSWhere\vswhere.exe -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
  set msbuild=%%i
)

"%msbuild%" /m SharpDevelop.sln /p:Configuration=Release "/p:Platform=Any CPU" %*


# Select-String -Path "d:\PT\3rds\SharpDevelop\itemType.txt" -Pattern "ItemType=(\w+)" | ForEach-Object { $_.Matches[0].Groups[1].Value } | Sort-Object -Unique | Out-File -FilePath "d:\PT\3rds\SharpDevelop\itemTypes_unique.txt" -Encoding UTF8
