param (
    $RepositoryRoot = "$PSScriptRoot/..",
    $UpstreamGitRemote = 'https://github.com/tdlib/td.git',
    $UpstreamRemoteName = 'upstream',
    $LocalBranchNameForUpstream = 'tdlib',
    $UpstreamMainBranchName = 'master'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Push-Location $RepositoryRoot
try {
    git remote add upstream $UpstreamGitRemote
    if (!$?) {  throw "Unable to add Git remote: exit code $LASTEXITCODE." }
    try {
        git fetch upstream
        if (!$?) { throw "Unable to fetch the upstream repository: exit code $LASTEXITCODE." }

        git branch --force $LocalBranchNameForUpstream "$UpstreamRemoteName/$UpstreamMainBranchName"
        if (!$?) { throw "Unable to create a branch ${LocalBranchNameForUpstream}: exit code $LASTEXITCODE." }
    } finally {
        git remote remove upstream
        if (!$?) { Write-Warning "Unable to remove Git remote: exit code $LASTEXITCODE." }
    }
} finally {
    Pop-Location
}
