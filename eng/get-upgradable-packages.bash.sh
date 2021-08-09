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
    local versionData=$(echo "$currentVersion,$upgradeVersion")
    upgradablePackageVersions[$pkgName]=$versionData

    printPackageInfo "$pkgName" "$currentVersion" "$upgradeVersion"
}

getUpgradablePackageVersionsForApt() {
    echo "Updating package cache..."
    apt update

    grep security /etc/apt/sources.list > /tmp/security.list

    # Find all upgradable packages from security feeds and store the output in an array
    mapfile -t aptPackages < <(apt upgrade -oDir::Etc::Sourcelist=/tmp/security.list -s 2>/dev/null | grep Inst | sort)

    # Regex to parse the output from apt to get the package name, current version, and upgrade version
    regex="Inst\s(\S+)\s\[(\S+)]\s\((\S+)\s"

    echo
    echo "Upgradable packages:"
    for pkg in "${aptPackages[@]}"
    do
        if [[ $pkg =~ $regex ]]; then
            pkgName=${BASH_REMATCH[1]}
            currentVersion=${BASH_REMATCH[2]}
            upgradeVersion=${BASH_REMATCH[3]}
            
            addUpgradeablePackageVersion "$pkgName" "$currentVersion" "$upgradeVersion"
        fi
    done
}

getUpgradablePackageVersionsForApk() {
    echo "Updating package cache..."
    apk update

    # Find all installed package names and store the output in an array
    mapfile -t apkPackageNames < <(apk info | sort)

    # Find all upgradable packages
    apkPackages=$(apk version | tail -n +2 | sort)

    echo
    echo "Upgradable packages:"
    for pkgName in "${apkPackageNames[@]}"
    do
        # Regex to parse the output from apk to get the package name, current version, and upgrade version
        regex="$pkgName-(\S+)\s+\S+\s+(\S+)"
        if [[ $apkPackages =~ $regex ]]; then
            currentVersion=${BASH_REMATCH[1]}
            upgradeVersion=${BASH_REMATCH[2]}
            
            addUpgradeablePackageVersion "$pkgName" "$currentVersion" "$upgradeVersion"
        fi
    done
}

getUpgradablePackageVersionsForTdnf() {
    echo "Updating package cache..."
    tdnf makecache

    # Find all installed packages and store the output in an array
    local installedPkgLines
    mapfile -t installedPkgLines < <(tdnf list installed | tail -n +2 | sort)

    # Find all upgradable packages
    local upgradePkgLines=$(tdnf list upgrades 2>/dev/null | tail -n +2 | sort)

    echo
    echo "Upgradable packages:"
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
    echo "Packages to upgrade:"

    local packagesToUpgrade=()
    local pkgName
    # Lookup the provided package names to see if any are in the list of upgradable packages
    for pkgName in "${packagesToCheck[@]}"
    do
        versionData=${upgradablePackageVersions[$pkgName]}

        if [ ! -z "$versionData" ]; then
            # Split versionData by comma
            versionArray=( ${versionData//,/ } )
            currentVersion=${versionArray[0]}
            upgradeVersion=${versionArray[1]}
            
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

packagesToCheck=( "${args[@]:1}" )

declare -A upgradablePackageVersions
upgradablePackageVersions=()

if type apt > /dev/null 2>/dev/null; then
    getUpgradablePackageVersionsForApt
    outputPackagesToUpgrade
    exit 0
fi

if type apk > /dev/null 2>/dev/null; then
    getUpgradablePackageVersionsForApk
    outputPackagesToUpgrade
    exit 0
fi

if type tdnf > /dev/null 2>/dev/null; then
    getUpgradablePackageVersionsForTdnf
    outputPackagesToUpgrade
    exit 0
fi

echo "Unsupported package manager. Current supported package managers: apt, apk, tdnf" >&2
exit 1
