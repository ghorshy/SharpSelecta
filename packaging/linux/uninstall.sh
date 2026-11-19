#!/bin/sh
# Removes what install.sh put in place. Your settings, library index and playlists, which live next to the
# app in ~/.local/share/SharpSelecta, stay where they are.
set -eu

id=io.github.ghorshy.SharpSelecta
data_dir=${XDG_DATA_HOME:-$HOME/.local/share}
app_dir=$data_dir/SharpSelecta/app
link=$HOME/.local/bin/sharpselecta

if [ -L "$link" ] && [ "$(readlink "$link")" = "$app_dir/sharpselecta" ]; then
    rm "$link"
fi
rm -f "$data_dir/applications/$id.desktop" "$data_dir/icons/hicolor/scalable/apps/$id.svg"
if [ -d "$app_dir" ]; then
    rm -r "$app_dir"
fi

update-desktop-database "$data_dir/applications" 2>/dev/null || true
gtk-update-icon-cache -f -t "$data_dir/icons/hicolor" 2>/dev/null || true

echo "SharpSelecta is removed. Your settings and library are still in $data_dir/SharpSelecta; delete that folder too if you don't want them."
