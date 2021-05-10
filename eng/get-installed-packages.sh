#!/usr/bin/env sh

# If package manager is apt
if type apt > /dev/null 2>/dev/null; then
    apt list --installed 2>/dev/null | grep installed | cut -d/ --fields=1 | sort
    exit 0
fi

# If package manager is apk
if type apk > /dev/null 2>/dev/null; then
    apk info | sort
    exit 0
fi

# If package manager is tdnf
if type tdnf > /dev/null 2>/dev/null; then
    tdnf list installed --quiet | cut -d. -f1 | tail -n +2 | sort
    exit 0
fi

echo "Unsupported package manager. Current supported package managers: apt, apk, tdnf" >&2
exit 1
