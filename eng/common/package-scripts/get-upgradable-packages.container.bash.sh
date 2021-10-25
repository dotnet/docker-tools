#!/usr/bin/env bash

# This is the Bash-based script for retrieving the list of upgradable packages

printPackageInfo() {
    echo $1
    echo "- Current: $2"
    echo "- Upgrade: $3"
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

installPackageWithApt() {
    if [[ $1 =~ $packageVersionRegex ]]; then
        local pkgName=${BASH_REMATCH[1]}
        local pkgVersion=${BASH_REMATCH[2]}

        apt show 2>/dev/null $1
        
        # If this package version exists for install
        if [[ $? == 0 ]]; then
            echo "Installing package $1"
            apt install -y $1
        else
            echo "Finding latest version of package $pkgName"
            local pkgInfo=$(apt policy $pkgName)

            # Get the candidate version of the package to be installed
            local candidateVersion=$(echo "$pkgInfo" | sed -n 's/.*Candidate:\s*\(\S*\)/\1/p')

            # Check if the candidate package version comes from a security repository
            apt-cache madison $pkgName | grep $candidateVersion | grep security

            # If the candidate version comes from a security repository, install the package
            if [[ $? == 0 ]]; then
                addUpgradeablePackageVersion "$pkgName" "$pkgVersion" "$candidateVersion"
            fi
        fi
    fi
}

getUpgradablePackageVersionsForApt() {
    grep security /etc/apt/sources.list > /tmp/security.list

    # Find all upgradable packages from security feeds and store the output in an array
    mapfile -t aptPackages < <(apt upgrade -oDir::Etc::Sourcelist=/tmp/security.list -s 2>/dev/null | grep Inst | sort)

    # Regex to parse the output from apt to get the package name, current version, and upgrade version
    local regex="Inst\s(\S+)\s\[(\S+)]\s\((\S+)\s"

    echo
    echo "Installed packages available to upgrade:"
    for pkg in "${aptPackages[@]}"
    do
        if [[ $pkg =~ $regex ]]; then
            local pkgName=${BASH_REMATCH[1]}
            local currentVersion=${BASH_REMATCH[2]}
            local upgradeVersion=${BASH_REMATCH[3]}
            
            addUpgradeablePackageVersion "$pkgName" "$currentVersion" "$upgradeVersion"
        fi
    done
}

installPackageWithApk() {
    if [[ $1 =~ $packageVersionRegex ]]; then
        local pkgName=${BASH_REMATCH[1]}
        local pkgVersion=${BASH_REMATCH[2]}
        apk list $pkgName | grep $pkgVersion
        
        # If this package version exists for install
        if [[ $? == 0 ]]; then
            echo "Installing package $1"
            apk add $1
        else
            echo "Finding latest version of package $pkgName"
            availableVersion=$(apk list $pkgName | tac | sed -n "1 s/$pkgName-\(\S*\).*/\1/p")
            addUpgradeablePackageVersion "$pkgName" "$pkgVersion" "$availableVersion"
        fi
    fi
}

getUpgradablePackageVersionsForApk() {
    # Find all installed package names and store the output in an array
    mapfile -t apkPackageNames < <(apk info | sort)

    # Find all upgradable packages
    local apkPackages=$(apk version | tail -n +2 | sort)

    echo
    echo "Installed packages available to upgrade:"
    for pkgName in "${apkPackageNames[@]}"
    do
        # Regex to parse the output from apk to get the package name, current version, and upgrade version
        local regex="$pkgName-(\S+)\s+\S+\s+(\S+)"
        if [[ $apkPackages =~ $regex ]]; then
            local currentVersion=${BASH_REMATCH[1]}
            local upgradeVersion=${BASH_REMATCH[2]}
            
            addUpgradeablePackageVersion "$pkgName" "$currentVersion" "$upgradeVersion"
        fi
    done
}

installPackageWithTdnf() {
    if [[ $1 =~ $packageVersionRegex ]]; then
        local pkgName=${BASH_REMATCH[1]}
        local pkgVersion=${BASH_REMATCH[2]}

        tdnf list available $pkgName | grep $pkgVersion
        
        # If this package version exists for install
        if [[ $? == 0 ]]; then
            echo "Installing package $1"
            tdnf install -y $1
        else
            echo "Finding latest version of package $pkgName"
            tdnf install -y $pkgName
            installedVersion=$(tdnf list installed $pkgName | tail -n +2 | sed -n 's/\S*\s*\(\S*\)\s*.*/\1/p')
            addUpgradeablePackageVersion "$pkgName" "$pkgVersion" "$installedVersion"
        fi
    fi
}

getUpgradablePackageVersionsForTdnf() {
    # Find all installed packages and store the output in an array
    local installedPkgLines
    mapfile -t installedPkgLines < <(tdnf list installed | tail -n +2 | sort)

    # Find all upgradable packages
    local upgradePkgLines=$(tdnf list upgrades 2>/dev/null | tail -n +2 | sort)

    echo
    echo "Installed packages available to upgrade:"
    for installedPackageLine in "${installedPkgLines[@]}"
    do
        # Regex to get the package name and version from the output of tdnf list
        local pkgListRegex="(\S+)\.\w+\s+(\S+)"
        if [[ $installedPackageLine =~ $pkgListRegex ]]; then
            local pkgName=${BASH_REMATCH[1]}
            local currentVersion=${BASH_REMATCH[2]}

            # Regex to get the package name and version from the output of tdnf list
            local upgradeRegex="$pkgName\.\w+\s+(\S+)"

            if [[ $upgradePkgLines =~ $upgradeRegex ]]; then
                local upgradeVersion=${BASH_REMATCH[1]}
                
                addUpgradeablePackageVersion "$pkgName" "$currentVersion" "$upgradeVersion"
            fi
        fi
    done
}

outputPackagesToUpgrade() {
    echo
    echo "Packages requiring an upgrade:"

    local packagesToUpgrade=()
    local pkg
    # Lookup the provided package names to see if any are in the list of upgradable packages
    for pkg in "${packages[@]}"
    do
        if [[ $pkg =~ $packageVersionRegex ]]; then
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
    apt update 1>/dev/null

    for pkgName in "${packages[@]}"
    do
        installPackageWithApt $pkgName
    done
    getUpgradablePackageVersionsForApt
    outputPackagesToUpgrade
    exit 0
fi

if type apk > /dev/null 2>/dev/null; then
    echo "Updating package cache..."
    apk update 1>/dev/null

    for pkgName in "${packages[@]}"
    do
        installPackageWithApk $pkgName
    done
    getUpgradablePackageVersionsForApk
    outputPackagesToUpgrade
    exit 0
fi

if type tdnf > /dev/null 2>/dev/null; then
    echo "Updating package cache..."
    tdnf makecache 1>/dev/null

    for pkgName in "${packages[@]}"
    do
        installPackageWithTdnf $pkgName
    done

    tdnf install -y ${packages[@]}
    getUpgradablePackageVersionsForTdnf
    outputPackagesToUpgrade
    exit 0
fi

echo "Unsupported package manager. Current supported package managers: apt, apk, tdnf" >&2
exit 1
