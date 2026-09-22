param([string]$LogName='club-resumed-build.log',[double]$MinimumFreeGB=1)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$progress=Join-Path $root 'docs/WORK_PROGRESS.md'
$flag=Join-Path $root 'Logs/capacity-stop.flag'
function FreeBytes {return (Get-PSDrive -Name ([IO.Path]::GetPathRoot($root).Substring(0,1))).Free}
function RecordStop([string]$reason){[IO.File]::WriteAllText($flag,$reason);[IO.File]::AppendAllText($progress,("`r`n容量監視による中断: "+(Get-Date -Format s)+' '+$reason+"`r`n"),(New-Object System.Text.UTF8Encoding($false)))}
if(Test-Path -LiteralPath $flag){throw 'Capacity stop is pending. Resume only after checking space and recording user authorization.'}
if((FreeBytes) -lt $MinimumFreeGB*1GB){RecordStop 'Not enough free disk space to begin.';throw 'Insufficient free space.'}
$env:DOTNET_PROCESSOR_COUNT='1';$env:DOTNET_gcServer='0';$env:EMCC_CORES='1';$env:BEE_BUILD_THREADS='1';$env:BINARYEN_CORES='1'
$editor='C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe'
$project=Join-Path $root 'Builds/Unity6Validation'
$log=Join-Path (Join-Path $root 'Logs') $LogName
$p=Start-Process $editor -ArgumentList @('-batchmode','-nographics','-quit','-job-worker-count','1','-projectPath',$project,'-executeMethod','RacerBuild.Web','-logFile',$log) -WindowStyle Hidden -PassThru
Write-Output ('Unity build PID '+$p.Id+'; capacity guard '+$MinimumFreeGB+' GB')
while(-not $p.HasExited){
 if((FreeBytes) -lt $MinimumFreeGB*1GB){
  $all=@(Get-CimInstance Win32_Process);$ids=New-Object 'System.Collections.Generic.HashSet[int]';[void]$ids.Add($p.Id)
  do{$more=$false;foreach($item in $all){if($ids.Contains([int]$item.ParentProcessId) -and $ids.Add([int]$item.ProcessId)){$more=$true}}}while($more)
  Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
  foreach($taskId in $ids){Stop-Process -Id $taskId -Force -ErrorAction SilentlyContinue}
  RecordStop ('Stopped build '+$p.Id+' below free-space threshold. Log: '+$LogName)
  throw 'Build stopped by capacity guard. Do not continue work until user resumes.'
 }
 Start-Sleep -Seconds 2;$p.Refresh()
}
$p.WaitForExit()
if(-not (Select-String -LiteralPath $log -Pattern 'COAST_RACER_BUILD_OK' -Quiet)){throw ('Unity build failed; inspect '+$log)}
Write-Output ('PASS Unity build. Free bytes: '+(FreeBytes))