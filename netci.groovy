import jobs.generation.Utilities

def project = GithubProject
def branch = GithubBranchName
def isPR = true
def platformList = ['Debian', 'NanoServer']

platformList.each { platform ->
    def newJobName = Utilities.getFullJobName(project, platform, isPR)

    def newJob = job(newJobName) {
        steps {
            if (platform == 'NanoServer') {
                batchFile("powershell -NoProfile -File .\\Microsoft.DotNet.ImageBuilder\\build.ps1 -CleanupDocker")
            }
            else {
                shell("docker build -t runner -f ./Microsoft.DotNet.ImageBuilder/Dockerfile.linux.runner --pull .")
                shell("docker run -v /var/run/docker.sock:/var/run/docker.sock runner pwsh -File ./Microsoft.DotNet.ImageBuilder/build.ps1 -CleanupDocker")
            }
        }
    }

    if (platform == 'NanoServer') {
        newJob.with {label('windows.10.amd64.serverrs3.open')}
    }
    else {
        Utilities.setMachineAffinity(newJob, 'Ubuntu16.04', 'latest-or-auto-docker')
    }

    Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
    Utilities.addGithubPRTriggerForBranch(newJob, branch, platform)
}
