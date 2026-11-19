#!/bin/sh
# Installs SharpSelecta for the current user, without root: the app goes to ~/.local/share/SharpSelecta/app
# (next to its settings and library), a `sharpselecta` command to ~/.local/bin, and the menu entry and icon
# to ~/.local/share. Run it again from a newer release to update; your settings and library are kept.
set -eu

id=io.github.ghorshy.SharpSelecta
source_dir=$(cd "$(dirname "$0")" && pwd)
data_dir=${XDG_DATA_HOME:-$HOME/.local/share}
app_dir=$data_dir/SharpSelecta/app
bin_dir=$HOME/.local/bin

if [ ! -x "$source_dir/sharpselecta" ]; then
    echo "Run this script from the extracted SharpSelecta folder." >&2
    exit 1
fi

if [ "$source_dir" != "$app_dir" ]; then
    # Copy into a fresh folder first, so a failed copy never leaves a half-replaced install behind.
    mkdir -p "$data_dir/SharpSelecta"
    staging=$(mktemp -d "$data_dir/SharpSelecta/app.XXXXXX")
    cp -R "$source_dir"/. "$staging"/
    if [ -d "$app_dir" ]; then
        rm -r "$app_dir"
    fi
    mv "$staging" "$app_dir"
fi

mkdir -p "$bin_dir" "$data_dir/applications" "$data_dir/icons/hicolor/scalable/apps"
ln -sf "$app_dir/sharpselecta" "$bin_dir/sharpselecta"

# The menu entry points at the app directly, so it works even when ~/.local/bin isn't on PATH.
sed "s|^Exec=sharpselecta|Exec=\"$app_dir/sharpselecta\"|" "$app_dir/$id.desktop" > "$data_dir/applications/$id.desktop"
cp "$app_dir/$id.svg" "$data_dir/icons/hicolor/scalable/apps/$id.svg"

update-desktop-database "$data_dir/applications" 2>/dev/null || true
gtk-update-icon-cache -f -t "$data_dir/icons/hicolor" 2>/dev/null || true

echo "SharpSelecta is installed. Start it from your application menu, or run: sharpselecta"
case ":$PATH:" in
    *":$bin_dir:"*) ;;
    *) echo "Note: $bin_dir isn't on your PATH, so the 'sharpselecta' command needs its full path." ;;
esac
echo "To remove it later, run: $app_dir/uninstall.sh"
