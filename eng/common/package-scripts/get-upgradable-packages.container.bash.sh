#!/usr/bin/env bash

# This is the Bash-based script for retrieving the list of upgradable packages

printPackageInfo() {
    echo $1
    echo "- Current: $2"
    echo "- Upgrade: $3"
}

writeError() {
    echo "Error: $1" >>/dev/stderr
    exit 1
}

addUpgradeablePackageVersion() {
    local pkgName=$1
    local currentVersion=$2
    local upgradeVersion=$3

    # If the package is not already in the list, add it
    if [[ -z ${upgradablePackageVersions[$pkgName]} ]]; then
        local versionData=$(echo "$currentVersion,$upgradeVersion")
        upgradablePackageVersions[$pkgName]=$versionData

        printPackageInfo "$pkgName" "$currentVersion" "$upgradeVersion"
    fi
}

checkForUpgradableVersionWithApt() {
    if [[ ! $1 =~ $packageVersionRegex ]]; then
        writeError "Package version info for '$1' must be in the form of <pkg-name>=<pkg-version>"
    fi

    local pkgName=${BASH_REMATCH[1]}
    local pkgVersion=${BASH_REMATCH[2]}

    echo "Finding latest version of package $pkgName"
    local pkgInfo=$(apt policy $pkgName 2>/dev/null)
    if [[ $pkgInfo == "" ]]; then
        writeError "Package '$pkgName' does not exist."
    fi

    # Get the candidate version of the package to be installed
    local candidateVersion=$(echo "$pkgInfo" | sed -n 's/.*Candidate:\s*\(\S*\)/\1/p')

    echo $candidateVersion
    echo $pkgVersion

    # If a newer version of the package is available
    if [[ $candidateVersion != $pkgVersion ]]; then
        # Check if the candidate package version comes from a security repository
        apt-cache madison $pkgName | grep $candidateVersion | grep security 1>/dev/null

        # If the candidate version comes from a security repository, add it to the list of upgradable packages
        if [[ $? == 0 ]]; then
            addUpgradeablePackageVersion "$pkgName" "$pkgVersion" "$candidateVersion"
        fi
    fi
}

checkForUpgradableVersionWithApk() {
    if [[ ! $1 =~ $packageVersionRegex ]]; then
        writeError "Package version info for '$1' must be in the form of <pkg-name>=<pkg-version>"
    fi

    local pkgName=${BASH_REMATCH[1]}
    local pkgVersion=${BASH_REMATCH[2]}\

    echo "Finding latest version of package $pkgName"
    availableVersion=$(apk list $pkgName | tac | sed -n "1 s/$pkgName-\(\S*\).*/\1/p")
    if [[ $availableVersion == "" ]]; then
        writeError "Package '$pkgName' does not exist."
    fi

    # If a newer version of the package is available
    if [[ $availableVersion != $pkgVersion ]]; then
        # If the package exists, add it to the list of upgradable packages
        if [[ $availableVersion != "" ]]; then
            addUpgradeablePackageVersion "$pkgName" "$pkgVersion" "$availableVersion"
        fi
    fi
}

checkForUpgradableVersionWithTdnf() {
    if [[ ! $1 =~ $packageVersionRegex ]]; then
        writeError "Package version info for '$1' must be in the form of <pkg-name>=<pkg-version>"
    fi

    local pkgName=${BASH_REMATCH[1]}
    local pkgVersion=${BASH_REMATCH[2]}

    echo "Finding latest version of package $pkgName"
    tdnf install -y $pkgName 1>/dev/null 2>/dev/null

    # If the package exists
    if [[ $? == 0 ]]; then
        local installedVersion=$(tdnf list installed $pkgName | tail -n +2 | sed -n 's/\S*\s*\(\S*\)\s*.*/\1/p')
        # If a newer version of the package is available
        if [[ $installedVersion != $pkgVersion ]]; then
            addUpgradeablePackageVersion "$pkgName" "$pkgVersion" "$installedVersion"
        fi
    else
        writeError "Package '$pkgName' does not exist."
    fi
}

outputPackagesToUpgrade() {
    echo
    echo "Packages requiring an upgrade:"

    local packagesToUpgrade=()
    local pkg
    # Lookup the provided package names to see if any are in the list of upgradable packages
    for pkg in "${packages[@]}"
    do
        if [[ ! $pkg =~ $packageVersionRegex ]]; then
            writeError "Unable to parse package version info. Value: $pkg"
        fi

        local pkgName=${BASH_REMATCH[1]}
        versionData=${upgradablePackageVersions[$pkgName]}

        if [ ! -z "$versionData" ]; then
            # Split versionData by comma
            local versionArray=( ${versionData//,/ } )
            local currentVersion=${versionArray[0]}
            local upgradeVersion=${versionArray[1]}
            
            packagesToUpgrade+=($(echo "$pkgName,$currentVersion,$upgradeVersion"))
            printPackageInfo "$pkgName" "$currentVersion" "$upgradeVersion"
        fi
    done

    local upgradeCount=${#packagesToUpgrade[@]}
    if [ $upgradeCount = 0 ]; then
        echo "<none>"
    fi

    local outputDir=$(dirname "$outputPath")
    mkdir -p $outputDir

    printf "%s\n" "${packagesToUpgrade[@]}" > $outputPath
}


outputPath="$1"
args=( $@ )

packages=( "${args[@]:1}" )

declare -A upgradablePackageVersions
upgradablePackageVersions=()

packageVersionRegex="(\S+)=(\S+)"

if type apt > /dev/null 2>/dev/null; then
    echo "Updating package cache..."
    apt update 1>/dev/null 2>/dev/null

    for pkgName in "${packages[@]}"
    do
        checkForUpgradableVersionWithApt $pkgName
    done
    outputPackagesToUpgrade
    exit 0
fi

if type apk > /dev/null 2>/dev/null; then
    echo "Updating package cache..."
    apk update 1>/dev/null

    for pkgName in "${packages[@]}"
    do
        checkForUpgradableVersionWithApk $pkgName
    done
    outputPackagesToUpgrade
    exit 0
fi

if type tdnf > /dev/null 2>/dev/null; then
    echo "Updating package cache..."
    tdnf makecache 1>/dev/null

    for pkgName in "${packages[@]}"
    do
        checkForUpgradableVersionWithTdnf $pkgName
    done
    outputPackagesToUpgrade
    exit 0
fi

echo "Unsupported package manager. Current supported package managers: apt, apk, tdnf" >&2
exit 1
