#!/bin/sh
# Plots the flight paths for tuning (needs ImageMagick):
#   GALAGA_PATHS=/tmp/paths dotnet test tests/AVAGalaga.Tests --filter PathTests && tools/plot_paths.sh /tmp/paths
set -e
dir=${1:-/tmp/paths}
k=2
args=""
colors="red lime dodgerblue yellow magenta cyan orange white"
i=0
for f in "$dir"/*.txt; do
  c=$(echo $colors | cut -d' ' -f$((i % 8 + 1)))
  pts=$(awk -F, -v k=$k '{printf "%.1f,%.1f ", $1*k+120, $2*k+120}' "$f")
  args="$args -stroke $c -fill none -draw \"polyline $pts\""
  i=$((i+1))
done
eval magick -size 688x816 xc:black -stroke gray -fill none -draw \"rectangle 120,120 568,696\" $args "$dir/paths.png"
echo "$dir/paths.png"
