#!/bin/sh
set -eu

if [ "$#" -ne 1 ]; then
    printf 'Usage: sh build.sh "/path/to/KSP Managed"\n' >&2
    exit 2
fi

project=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
managed=$(CDPATH= cd -- "$1" && pwd)
compiler=${CSC:-mcs}

if ! command -v "$compiler" >/dev/null 2>&1; then
    printf 'Install Mono to get mcs, or set CSC to a compatible C# compiler executable.\n' >&2
    exit 1
fi

for assembly in mscorlib System Assembly-CSharp UnityEngine.CoreModule UnityEngine.PhysicsModule; do
    if [ ! -f "$managed/$assembly.dll" ]; then
        printf 'Missing assembly %s.dll in %s\n' "$assembly" "$managed" >&2
        exit 1
    fi
done

output="$project/GameData/ProjectSundial/Plugins/ProjectSundial.dll"
mkdir -p "$(dirname "$output")"
"$compiler" -nologo -langversion:6 -target:library -optimize+ -nostdlib \
    -r:"$managed/mscorlib.dll" \
    -r:"$managed/System.dll" \
    -r:"$managed/Assembly-CSharp.dll" \
    -r:"$managed/UnityEngine.CoreModule.dll" \
    -r:"$managed/UnityEngine.PhysicsModule.dll" \
    -out:"$output" "$project"/Source/*.cs
printf 'Built %s\n' "$output"
