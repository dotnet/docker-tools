#!/usr/bin/env sh

# This is the main POSIX-compliant script for retrieving the list of upgradable packages. It's just a wrapper around
# the Bash-based script. This ensures that Bash is installed and then runs the Bash script.

ensureBashInstalledForApt() {
    echo "Ensuring bash is installed"
    apt update
    apt install -y bash
}

ensureBashInstalledForApk() {
    echo "Ensuring bash is installed"
    apk add bash
}

ensureBashInstalledForTdnf() {
    echo "Ensuring bash is installed"
    tdnf makecache
    tdnf install -y bash
}



scriptDir=$(dirname $0)

if type apt > /dev/null 2>/dev/null; then
    ensureBashInstalledForApt
    $scriptDir/get-upgradable-packages.bash.sh $@
    exit 0
fi

if type apk > /dev/null 2>/dev/null; then
    ensureBashInstalledForApk
    $scriptDir/get-upgradable-packages.bash.sh $@
    exit 0
fi

if type tdnf > /dev/null 2>/dev/null; then
    ensureBashInstalledForTdnf
    $scriptDir/get-upgradable-packages.bash.sh $@
    exit 0
fi

echo "Unsupported package manager. Current supported package managers: apt, apk, tdnf" >&2
exit 1
