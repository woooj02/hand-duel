param([Parameter(Mandatory=$true)][string]$Unity)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$log=Join-Path $project 'Builds/build.log'
New-Item -ItemType Directory -Path (Split-Path $log -Parent) -Force | Out-Null
if(!(Test-Path -LiteralPath $Unity)){throw 'Supply the full path to Unity 6000.0.66f2 Unity.exe.'}
$buildArgs=@('-batchmode','-nographics','-quit','-buildTarget','Android','-projectPath',('"{0}"' -f $project),'-executeMethod','ShowOfHands.EditorTools.ButtonFixBuild.Build','-logFile',('"{0}"' -f $log))
$buildProcess=Start-Process -FilePath $Unity -ArgumentList $buildArgs -PassThru -Wait -WindowStyle Hidden
if($buildProcess.ExitCode -ne 0){throw "Build failed (exit $($buildProcess.ExitCode)). Read $log"}
$apk=Join-Path $project 'Builds/ShowOfHands.apk'
if(!(Test-Path -LiteralPath $apk)){throw 'Unity returned without producing the APK.'}
Write-Output $apk
